;;; Copyright (c) 2006, 2007 Abdulaziz Ghuloum and Kent Dybvig
;;; Copyright (c) 2008 Llewellyn Pritchard
;;; 
;;; Permission is hereby granted, free of charge, to any person obtaining a
;;; copy of this software and associated documentation files (the "Software"),
;;; to deal in the Software without restriction, including without limitation
;;; the rights to use, copy, modify, merge, publish, distribute, sublicense,
;;; and/or sell copies of the Software, and to permit persons to whom the
;;; Software is furnished to do so, subject to the following conditions:
;;; 
;;; The above copyright notice and this permission notice shall be included in
;;; all copies or substantial portions of the Software.
;;; 
;;; THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
;;; IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
;;; FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
;;; THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
;;; LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
;;; FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
;;; DEALINGS IN THE SOFTWARE. 

(library (psyntax main)
  (export
    trace-printer
    command-line
    load
    load-port
    load/args
    load/unload
    ironscheme-build
    ironscheme-test
    emacs-mode?
    display-stacktrace
    compile
    compile-system-libraries
    compile->closure)
  (import 
    (rnrs base)
    (rnrs exceptions)
    (rnrs control)
    (rnrs io simple)
    (rnrs io ports)
    (rnrs lists)
    (rnrs hashtables)
    (rnrs bytevectors)
    (only (rnrs conditions) serious-condition? condition?)
    (only (rnrs exceptions) raise raise-continuable with-exception-handler)
    (psyntax compat)
    (psyntax internal)
    (psyntax library-manager)
    (psyntax expander)
    (only (ironscheme core) get-command-line format compile-library load-library-dll generate-executable-wrapper compress-constants?)
    (ironscheme enums)
    (ironscheme files)
    (ironscheme clr)
    (only (ironscheme unsafe) $fx+ $fx-)
    (ironscheme constant-fold)
    (except (ironscheme library) file-locator alternative-file-locator)
    (only (ironscheme) printf pretty-print initialize-default-printers debug-mode? serialize-port deserialize-port time time-it string=?))
    
  (clr-reference System)
    
  (define trace-printer (make-parameter pretty-print))
      
  (define display-stacktrace (make-parameter #f))
  
  (define command-line (make-parameter (get-command-line))) 
   
  (define emacs-mode? (make-parameter #f))
     
  (define (local-library-path filename)
    (cons (get-directory-name filename) (library-path)))

  (define (load/args filename . args)
    (with-guard
      (lambda ()
        (apply load-r6rs-top-level filename 'load args)
        (void))))
    
  (define (load/unload filename)
    (let ((libs #f)) 
      (dynamic-wind
        (lambda ()
          (set! libs (installed-libraries)))
        (lambda ()
          (load filename))
        (lambda ()          
          (for-each
            (lambda (lib)
              (unless (memq lib libs)
                (uninstall-library lib)))
            (installed-libraries))))))

  (define (load filename)
    (with-guard
      (lambda ()
        (apply load-r6rs-top-level filename 'load (cdr (command-line)))
        (void))))
    
  (define (ironscheme-test)
    (let ((path (string-append (clr-static-prop-get IronScheme.Runtime.Builtins ApplicationDirectory)
                               "/tests/r6rs/run.sps")))
      (time-it "R6RS test suite"
        (lambda () 
          (load path)))))
      
  (define (ironscheme-build)
    (load "ironscheme-buildscript.sps"))
    
  (define foreground-color
    (case-lambda
      [()           (and (not (emacs-mode?)) (clr-static-prop-get Console ForegroundColor))]
      [(color)      (and (not (emacs-mode?)) (clr-static-prop-set! Console ForegroundColor color))])) 
      
  (define (system-exception? e)
    (clr-is SystemException e))  
    
  (define (eval-top-level x)
    (clr-guard [e [e (parameterize ((foreground-color 'red)
                                    (current-output-port (current-error-port)))
                       (display "Unhandled CLR exception during evaluation:\n")
                       (display e)
                       (newline))]]
      (call/cc
        (lambda (k)
          (with-exception-handler
            (lambda (e)
              (let ((serious? (or (serious-condition? e) (system-exception? e) (not (condition? e)))))
                (parameterize ((foreground-color (if serious? 'red 'yellow))
                               (current-output-port (current-error-port)))
                  (when serious?
                    (display "Unhandled exception during evaluation:\n"))
                  (display e)
                  (newline))
                (k)))
            (lambda ()
              (parameterize ([allow-library-redefinition #t])
                (eval x (interaction-environment)))))))))
            
  (define (eval-embedded x)
    (eval x (interaction-environment)))            
    
  (define compile-system-libraries
    (case-lambda
      [()
        (compile-system-libraries #f)]
      [(constant-compression?)
        (time-it "total compile time"
          (lambda ()
            (eval-top-level 
              `(begin
                 (include "system-libraries.ss")
                 (compile "system-libraries.ss" #f ,constant-compression?)))))]))
               
  (define (with-guard f)
    (clr-guard [e [e (parameterize ((current-output-port (current-error-port)))
                       (display "Unhandled CLR exception during evaluation:\n")
                       (display e)
                       (newline))]]
      (call/cc
        (lambda (k)
          (with-exception-handler
            (lambda (e)
              (let ((serious? (or (serious-condition? e) (system-exception? e) (not (condition? e)))))
                (parameterize ((current-output-port (current-error-port)))
                  (when serious?
                    (display "Unhandled exception during evaluation:\n"))
                  (display e))
                (k)))
            f)))))
    
  (define compile
    (case-lambda
      [(filename)
        (compile filename #f)]
      [(filename gen-wrapper?)
        (compile filename gen-wrapper? #f)]        
      [(filename gen-wrapper? constant-compression?)
        (with-guard
          (lambda ()
            (parameterize [(compress-constants? constant-compression?)]
              (load-r6rs-top-level filename 'compile-dll)
              (when gen-wrapper?
                (generate-executable-wrapper filename)))))]))
    
  (define (compile->closure filename)
    (load-r6rs-top-level filename 'closure))
    
  (define (load-port port . args)
    (with-guard
      (lambda ()
        (load-port-r6rs-top-level port #f 'load args)
        (void))))
       
  (define (load-port-r6rs-top-level port close-port? how args)
    (let ((x* (let f ()
                (let ((x (read-annotated port)))
                   (if (eof-object? x) 
                       '()
                       (cons x (f)))))))
      (when close-port?
        (close-input-port port))
      (case how
        ((closure)   (pre-compile-r6rs-top-level x*))
        ((load)      
          (parameterize ([command-line (cons (format "~a" port) (map (lambda (x) (format "~a" x)) args))])
            ((compile-r6rs-top-level x*)))))))
    
  (define (load-r6rs-top-level filename how . args)
    (parameterize ([library-path (local-library-path filename)])
      (let ((x* 
             (call-with-input-file filename
               (lambda (p)
                 (let f ()
                   (let ((x (read-annotated p)))
                     (if (eof-object? x) 
                         '()
                         (cons x (f)))))))))
        (case how
          ((closure)   (pre-compile-r6rs-top-level x*))
          ((load)      
            (parameterize ([command-line (cons filename (map (lambda (x) (format "~a" x)) args))])
              ((compile-r6rs-top-level x*))))
          ((compile-dll)
  		      (compile-r6rs-top-level x*)
			      (serialize-all compile-dll compile-core-expr))))))

  (define fo (make-enumeration '(no-fail no-create no-truncate)))
 
  (define (load-library-from-dll filename sk)        
    (let ((dll-filename (change-extension filename ".dll")))
      (if (file-exists? dll-filename)
          (if (or (not (file-exists? filename))
                  (file-newer? dll-filename filename))
              (clr-guard [e 
                      (e 
                        (display (format "WARNING: precompiled library (~a) could not load.\n" 
                                         (relative-filename filename)) 
                                 (current-error-port))
                        #f)]
                (let ((content (load-library-dll dll-filename)))
                  (and content (apply sk content))))
              (begin
                (display (format "WARNING: precompiled library (~a) is out of date.\n" 
                                 (relative-filename filename)) 
                         (current-error-port))
                #f))
          #f)))
                    
  (define (compile-dll filename content)
    (display "compiling ")
    (display (relative-filename filename))
    (newline)
    (let ((v (list->vector content)))
      (vector-set! v 0 `',(vector-ref v 0))
      (vector-set! v 1 `',(vector-ref v 1))
      (vector-set! v 2 `',(vector-ref v 2))
      (vector-set! v 3 `',(vector-ref v 3))
      (vector-set! v 4 `',(vector-ref v 4))
      (vector-set! v 5 `',(vector-ref v 5))
      (vector-set! v 6 `',(vector-ref v 6))
      (vector-set! v 7 `',(vector-ref v 7))
      (vector-set! v 8 `(lambda () ,(vector-ref v 8)))
      (vector-set! v 9 `(lambda () ,(vector-ref v 9)))
      (vector-set! v 10 `(lambda () ,(vector-ref v 10)))
      (vector-set! v 11 `',(vector-ref v 11))
      (vector-set! v 12 `',(vector-ref v 12))
      (compile-library filename (compile-core-expr (cons 'list (vector->list v))))))
      
  (define (web-path-exists? url save-path)
    (if (string=? (substring url 0 5) "http:")
        (let ((wc (clr-new System.Net.WebClient)))
          (clr-guard [e [e #f]]
            (clr-call System.Net.WebClient DownloadFile  wc (clr-cast String url) save-path)
            (printf "Downloaded ~a to ~a\n" url save-path)
            #t))
        #f))
        
  (alternative-file-locator web-path-exists?)

  (current-precompiled-library-loader load-library-from-dll)
  
  (initialize-default-printers)
  
  ; hacks to get compiled libraries playing nicely
  (set-symbol-value! 'list list)
  (set-symbol-value! 'values values)    
  
  (set-symbol-value! 'load load)
  (set-symbol-value! 'compile compile)
  (set-symbol-value! 'compile->closure compile->closure)
  (set-symbol-value! 'eval-r6rs eval-top-level)
  (set-symbol-value! 'eval-embedded eval-embedded)
  (set-symbol-value! 'int-env-syms interaction-environment-symbols)
  (set-symbol-value! 'expanded2core expanded->core)
  
  (set-symbol-value! 'trace-printer trace-printer)
  (set-symbol-value! 'pretty-print pretty-print)
  (set-symbol-value! 'assertion-violation assertion-violation)
  (set-symbol-value! 'raise raise)
  (set-symbol-value! 'raise-continuable raise-continuable)
  (set-symbol-value! 'with-exception-handler with-exception-handler)
  (set-symbol-value! 'emacs-mode? emacs-mode?)
  (set-symbol-value! 'stacktrace-enable? display-stacktrace)
  
  (file-options-constructor (enum-set-constructor fo))
  
  (library-path (get-library-paths))
  
  (library-extensions (append (library-extensions) (list ".ironscheme.sls")))
  
  (unless (debug-mode?)
    (enable-constant-fold/env 
      '(only (rnrs) + - * / = < > <= >= negative? positive? zero? exp expt div mod div0 mod0 even? odd? 
                    log abs tan sin cos atan asin acos angle denominator)
      '(except (rnrs arithmetic fixnums) fx*/carry fx-/carry fx+/carry fxdiv0-and-mod0 fxdiv-and-mod)
      '(except (rnrs arithmetic flonums) fldiv0-and-mod0 fldiv-and-mod 
                make-no-infinities-violation make-no-nans-violation 
                no-infinities-violation? no-nans-violation?)
      '(rnrs arithmetic bitwise)))
  
  (interaction-environment (new-interaction-environment))
  
  (set-symbol-value! 'r6rs-loaded #t))