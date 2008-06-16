#region License
/* ****************************************************************************
 * Copyright (c) Llewellyn Pritchard. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. 
 * A copy of the license can be found in the License.html file at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 * ***************************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using System.Reflection;

namespace IronScheme.Runtime
{
  public delegate object ConsFromArrayHandler(object[] args);
  public delegate object[] ArrayFromConsHandler(object args);
  public delegate object AssertHandler(object who, object msg, params object[] irritants);


  public abstract class Closure : ICallable
  {
    readonly static Dictionary<Type, int> targetmap = new Dictionary<Type, int>();

    static Closure()
    {
      targetmap.Add(typeof(CallTarget0), 0);
      targetmap.Add(typeof(CallTarget1), 1);
      targetmap.Add(typeof(CallTarget2), 2);
      targetmap.Add(typeof(CallTarget3), 3);
      targetmap.Add(typeof(CallTarget4), 4);
      targetmap.Add(typeof(CallTarget5), 5);
      targetmap.Add(typeof(CallTargetN), -1);

      targetmap.Add(typeof(CallTargetWithContext0), 0 + 8);
      targetmap.Add(typeof(CallTargetWithContext1), 1 + 8);
      targetmap.Add(typeof(CallTargetWithContext2), 2 + 8);
      targetmap.Add(typeof(CallTargetWithContext3), 3 + 8);
      targetmap.Add(typeof(CallTargetWithContext4), 4 + 8);
      targetmap.Add(typeof(CallTargetWithContext5), 5 + 8);
      targetmap.Add(typeof(CallTargetWithContextN), -1 + 8);
    }

    public static AssertHandler AssertionViolation;

    int paramcount = int.MaxValue;

    public static ConsFromArrayHandler ConsFromArray;

    public override string ToString()
    {
      if (target == null || target.Method == null)
      {
        return "unknown closure";
      }
      return target.Method.Name;
    }

    public virtual int Arity
    {
      get { return paramcount; }
    }

    public virtual object Call()
    {
      return Call(new object[0]);
    }

    public virtual object Call(object arg1)
    {
      return Call(new object[] { arg1 });
    }

    public virtual object Call(object arg1, object arg2)
    {
      return Call(new object[] { arg1, arg2 });
    }

    public virtual object Call(object arg1, object arg2, object arg3)
    {
      return Call(new object[] { arg1 , arg2, arg3 });
    }

    public virtual object Call(object arg1, object arg2, object arg3, object arg4)
    {
      return Call(new object[] { arg1 , arg2, arg3, arg4 });
    }

    public virtual object Call(object arg1, object arg2, object arg3, object arg4, object arg5)
    {
      return Call(new object[] { arg1, arg2, arg3, arg4, arg5 });
    }

    public abstract object Call(object[] args);

    readonly Delegate target;

    public virtual MethodInfo Target
    {
      get { return target.Method; }
    }

    protected Closure() : this(null, -1)
    {
    }

    Closure(Delegate target, int paramcount)
    {
      this.paramcount = paramcount;
      this.target = target;
    }

    protected object GetWho()
    {
      return SymbolTable.StringToId(target.Method.Name);
    }

    sealed class ContextClosure : Closure
    {
      CodeContext cc;

      public ContextClosure(CodeContext cc, Delegate target, int paramcount)
        : base(target, paramcount)
      {
        this.cc = cc;
      }

      public override object Call(object[] args)
      {
        if (paramcount >= 0 && paramcount != args.Length)
        {
          AssertionViolation(GetWho(), string.Format("invalid argument count, expected {0} got {1}", paramcount, args.Length), args);
        }
        switch (paramcount)
        {
          case -1:
            return ((CallTargetWithContextN)target)(cc, args);
          case 0:
            return ((CallTargetWithContext0)target)(cc);
          case 1:
            return ((CallTargetWithContext1)target)(cc, args[0]);
          case 2:
            return ((CallTargetWithContext2)target)(cc, args[0], args[1]);
          case 3:
            return ((CallTargetWithContext3)target)(cc, args[0], args[1], args[2]);
          case 4:
            return ((CallTargetWithContext4)target)(cc, args[0], args[1], args[2], args[3]);
          case 5:
            return ((CallTargetWithContext5)target)(cc, args[0], args[1], args[2], args[3], args[4]);
          default:
            throw new NotSupportedException();
        }
      }

      public override object Call()
      {
        if (paramcount == 0)
        {
          return ((CallTargetWithContext0)target)(cc);
        }
        else
        {
          return base.Call();
        }
      }

      public override object Call(object arg1)
      {
        if (paramcount == 1)
        {
          return ((CallTargetWithContext1)target)(cc, arg1);
        }
        else
        {
          return base.Call(arg1);
        }
      }

      public override object Call(object arg1, object arg2)
      {
        if (paramcount == 2)
        {
          return ((CallTargetWithContext2)target)(cc, arg1, arg2);
        }
        else
        {
          return base.Call(arg1, arg2);
        }
      }

      public override object Call(object arg1, object arg2, object arg3)
      {
        if (paramcount == 3)
        {
          return ((CallTargetWithContext3)target)(cc, arg1, arg2, arg3);
        }
        else
        {
          return base.Call(arg1, arg2, arg3);
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4)
      {
        if (paramcount == 4)
        {
          return ((CallTargetWithContext4)target)(cc, arg1, arg2, arg3, arg4);
        }
        else
        {
          return base.Call(arg1, arg2, arg3, arg4);
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4, object arg5)
      {
        if (paramcount == 5)
        {
          return ((CallTargetWithContext5)target)(cc, arg1, arg2, arg3, arg4, arg5);
        }
        else
        {
          return base.Call(arg1, arg2, arg3, arg4, arg5);
        }
      }
    }

    sealed class SimpleClosure : Closure
    {
      public SimpleClosure(Delegate target, int paramcount)
        : base(target, paramcount)
      {
      }

      public override object Call(object[] args)
      {
        if (paramcount >= 0 && paramcount != args.Length)
        {
          AssertionViolation(GetWho(), string.Format("invalid argument count, expected {0} got {1}", paramcount, args.Length), args);
        }
        switch (paramcount)
        {
          case -1:
            return ((CallTargetN)target)(args);
          case 0:
            return ((CallTarget0)target)();
          case 1:
            return ((CallTarget1)target)(args[0]);
          case 2:
            return ((CallTarget2)target)(args[0], args[1]);
          case 3:
            return ((CallTarget3)target)(args[0], args[1], args[2]);
          case 4:
            return ((CallTarget4)target)(args[0], args[1], args[2], args[3]);
          case 5:
            return ((CallTarget5)target)(args[0], args[1], args[2], args[3], args[4]);
          default:
            throw new NotSupportedException();
        }
      }

      public override object Call()
      {
        if (paramcount == 0)
        {
          return ((CallTarget0)target)();
        }
        else
        {
          return base.Call();
        }
      }

      public override object Call(object arg1)
      {
        if (paramcount == 1)
        {
          return ((CallTarget1)target)(arg1);
        }
        else
        {
          return base.Call(arg1);
        }
      }

      public override object Call(object arg1, object arg2)
      {
        if (paramcount == 2)
        {
          return ((CallTarget2)target)(arg1, arg2);
        }
        else
        {
          return base.Call(arg1, arg2);
        }
      }

      public override object Call(object arg1, object arg2, object arg3)
      {
        if (paramcount == 3)
        {
          return ((CallTarget3)target)(arg1, arg2, arg3);
        }
        else
        {
          return base.Call(arg1, arg2, arg3);
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4)
      {
        if (paramcount == 4)
        {
          return ((CallTarget4)target)(arg1, arg2, arg3, arg4);
        }
        else
        {
          return base.Call(arg1, arg2, arg3, arg4);
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4, object arg5)
      {
        if (paramcount == 5)
        {
          return ((CallTarget5)target)(arg1, arg2, arg3, arg4, arg5);
        }
        else
        {
          return base.Call(arg1, arg2, arg3, arg4, arg5);
        }
      }
    }

    public static ICallable Make(CodeContext cc, Delegate target)
    {
      int arity;
      if (targetmap.TryGetValue(target.GetType(), out arity))
      {
        if (arity < 6 && arity > -2) // no context
        {
          return new SimpleClosure(target, arity);
        }
        else
        {
          arity -= 8;
          return new ContextClosure(cc, target, arity);
        }
      }
      throw new NotSupportedException();
    }

    public static ICallable MakeVarArgX(CodeContext cc, Delegate target, int paramcount)
    {
      return new VarArgClosure(cc, target, paramcount);
    }

    public static ICallable MakeCase(CodeContext cc, Delegate[] targets, int[] arities)
    {
      return new CaseClosure(cc, targets, arities);
    }

    sealed class VarArgClosure : Closure
    {
      ICallable realtarget;
      int pcount = 0;

      public override MethodInfo Target
      {
        get { return null; }
      }

      public VarArgClosure(CodeContext cc, Delegate target, int paramcount)
        : base(target, -1)
      {
        pcount = paramcount;
        realtarget = Make(cc, target);
      }

      public override int Arity
      {
        get { return pcount - 1; }
      }

      public override object Call(object[] args)
      {
        if (args.Length + 1 < pcount)
        {
          AssertionViolation(realtarget.ToString(), string.Format("invalid argument count, expected at least {0} got {1}", pcount, args.Length), args);
        }
        object[] newargs = new object[pcount];
        Array.Copy(args, newargs, pcount - 1);
        object[] last = new object[args.Length - pcount + 1];
        Array.Copy(args, pcount - 1, last, 0, last.Length);
        newargs[pcount - 1] = ConsFromArray(last);
        return realtarget.Call(newargs);
      }

      public override string ToString()
      {
        return realtarget.ToString();
      }
    }



    sealed class CaseClosure : Closure
    {
      int[] arities;
      List<ICallable> targets = new List<ICallable>();

      public override MethodInfo Target
      {
        get { return null; }
      }

      public CaseClosure(CodeContext cc, Delegate[] targets, int[] arities)
        : base(null, -1)
      {
        this.arities = arities;
        for (int i = 0; i < targets.Length; i++)
        {
          if (arities[i] < 0)
          {
            this.targets.Add(MakeVarArgX(cc, targets[i], -arities[i]));
          }
          else
          {
            this.targets.Add(Make(cc, targets[i]));
          }
        }
      }

      public override int Arity
      {
        get 
        {
          int a = int.MaxValue;
          foreach (ICallable c in targets)
          {
            a = Math.Min(c.Arity, a);
          }
          return a;
        }
      }

      public override string ToString()
      {
        foreach (ICallable c in targets)
        {
          return c.ToString();
        }
        return "empty case-lambda";
      }

      public override object Call(object[] args)
      {
        int arglen = args.Length;

        for (int i = 0; i < arities.Length; i++)
        {
          int a = arities[i];
          if (a == arglen || (a < 0 && arglen >= -a - 1))
          {
            return targets[i].Call(args);
          }
        }

        return AssertionViolation(targets[0].ToString(), "invalid argument count", args, arities);
      }

      public override object Call()
      {
        int i = Array.IndexOf(arities, 0);
        if (i >= 0)
        {
          return targets[i].Call();
        }
        else
        {
          return Call(new object[0]);
        }
      }

      public override object Call(object arg1)
      {
        int i = Array.IndexOf(arities, 1);
        if (i >= 0)
        {
          return targets[i].Call(arg1);
        }
        else
        {
          return Call(new object[] { arg1 });
        }
      }

      public override object Call(object arg1, object arg2)
      {
        int i = Array.IndexOf(arities, 2);
        if (i >= 0)
        {
          return targets[i].Call(arg1, arg2);
        }
        else
        {
          return Call(new object[] { arg1, arg2 });
        }
      }

      public override object Call(object arg1, object arg2, object arg3)
      {
        int i = Array.IndexOf(arities, 3);
        if (i >= 0)
        {
          return targets[i].Call(arg1, arg2, arg3);
        }
        else
        {
          return Call(new object[] { arg1, arg2, arg3 });
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4)
      {
        int i = Array.IndexOf(arities, 4);
        if (i >= 0)
        {
          return targets[i].Call(arg1, arg2, arg3, arg4);
        }
        else
        {
          return Call(new object[] { arg1, arg2, arg3, arg4 });
        }
      }

      public override object Call(object arg1, object arg2, object arg3, object arg4, object arg5)
      {
        int i = Array.IndexOf(arities, 5);
        if (i >= 0)
        {
          return targets[i].Call(arg1, arg2, arg3, arg4, arg5);
        }
        else
        {
          return Call(new object[] { arg1, arg2, arg3, arg4, arg5 });
        }
      }
    }


  }


}
