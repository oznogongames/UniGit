﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UniGit.Utils
{
	public class InjectionHelper
	{
		private readonly List<Resolve> resolves;
	    private InjectionHelper parent;

		public InjectionHelper()
		{
			resolves = new List<Resolve>();
		    Bind<InjectionHelper>().FromInstance(this);
		}

		public void Clear()
		{
			resolves.Clear();
		}

		public Resolve Bind(Type type)
		{
			var resolve = new Resolve(this,type);
			resolves.Add(resolve);
			return resolve;
		}

		public Resolve<T> Bind<T>()
		{
			var resolve = new Resolve<T>(this);
			resolves.Add(resolve);
			return resolve;
		}

	    public void SetParent(InjectionHelper parent)
	    {
	        this.parent = parent;
	    }

		public T CreateInstance<T>()
		{
			return (T)CreateInstance(typeof(T));
		}

		public object CreateInstance(Type type)
		{
			var constructor = GetInjectConstructor(type);
			if (constructor != null)
			{
				var parameterInfos = constructor.GetParameters();
				object[] args = new object[parameterInfos.Length];
				for (int i = 0; i < parameterInfos.Length; i++)
				{
					args[i] = HandleParameter(parameterInfos[i], type);
				}
				object instance = constructor.Invoke(args);
				return instance;
			}
			return Activator.CreateInstance(type);
		}

		public void Inject(object obj)
		{
			Type type = obj.GetType();
			List<MethodInfo> infos = GetInjectMethods(type);
			foreach (var methodInfo in infos)
			{
				var parameterInfos = methodInfo.GetParameters();
				object[] args = new object[parameterInfos.Length];
				for (int i = 0; i < parameterInfos.Length; i++)
				{
					args[i] = HandleParameter(parameterInfos[i], type);
				}
				try
				{
					methodInfo.Invoke(obj, args);
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("There was a problem while calling injectable method {0}",methodInfo.Name);
					Debug.LogException(e);
					return;
				}
			}
		}

		private List<MethodInfo> GetInjectMethods(Type type)
		{
			List<MethodInfo> infos = new List<MethodInfo>();
			Type lastType = type;
			while (lastType != null)
			{
				var methods = lastType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (var method in methods)
				{
					if (HasInjectAttribute(method))
					{
                        if(method.IsAbstract) continue;
                        //don't put overriden methods, only methods that are virtual
						if (method.IsVirtual && method.GetBaseDefinition().DeclaringType != lastType) continue;
						infos.Add(method);
					}
				}
				lastType = lastType.BaseType;
			}
			infos.Reverse();
			return infos;
		}

		private bool HasInjectAttribute(MethodInfo methodInfo)
		{
			return methodInfo.GetCustomAttributes(typeof(UniGitInject), true).Length > 0;
		}

		private ConstructorInfo GetInjectConstructor(Type type)
		{
			var constructors = type.GetConstructors();
			return constructors.FirstOrDefault(c => c.GetCustomAttributes(typeof(UniGitInject), true).Length > 0);
		}

		private void CheckCrossDependency(Resolve resolve, Type injectedType,ParameterInfo parameterInfo)
		{
			if (resolve.injectedParamsCached == null)
				resolve.injectedParamsCached = BuildInjectionParams(GetInjectConstructor(resolve.InstanceType));
			if (CrossDependency(resolve.injectedParamsCached, injectedType))
				throw new Exception(string.Format("Cross References detected when injecting parameter {0} of '{1}' with '{2}'", parameterInfo.Name,injectedType.Name, resolve.InstanceType.Name));
		}

		private bool CrossDependency(KeyValuePair<string,Type>[] injectedParams, Type type)
		{
			if (injectedParams == null) return false;

			foreach (var param in injectedParams)
			{
				if (param.Value == type || param.Value.IsAssignableFrom(type) || type.IsAssignableFrom(param.Value))
				{
					return true;
				}
			}

			return false;
		}

		private KeyValuePair<string, Type>[] BuildInjectionParams(ConstructorInfo constructorInfo)
		{
			if(constructorInfo == null)
				return new KeyValuePair<string, Type>[0];
			return constructorInfo.GetParameters().Select(p => new KeyValuePair<string, Type>(p.Name, p.ParameterType)).ToArray();
		}

		private bool IsGenericTypeList(Type type)
		{
			return typeof(ICollection<>).IsAssignableFrom(type) || typeof(IList<>).IsAssignableFrom(type) || typeof(List<>).IsAssignableFrom(type);
		}

		private object HandleParameter(ParameterInfo parameter,Type injecteeType)
		{
			if (parameter.ParameterType.IsGenericType)
			{
				Type parameterGenericType = parameter.ParameterType.GetGenericTypeDefinition();
				if (IsGenericTypeList(parameterGenericType))
				{
					var listType = parameter.ParameterType.GetGenericArguments()[0];
					List<Resolve> parameterResolves = new List<Resolve>();
					IList instanceCollection = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));
					if (FindResolves(listType, parameter.Name, injecteeType, parameterResolves))
					{
						foreach (var r in parameterResolves)
						{
							CheckCrossDependency(r, injecteeType, parameter);
							instanceCollection.Add(r.GetInstance());
						}
					}
					return instanceCollection;
				}
			}

			Resolve resolve;
			if (FindResolve(parameter, injecteeType, out resolve))
			{
				CheckCrossDependency(resolve, injecteeType, parameter);
				return resolve.GetInstance();
			}

		    if (parent != null)
		    {
		        return parent.HandleParameter(parameter, injecteeType);
		    }

			throw new Exception(string.Format("Unresolved parameter: {0} with type: {1}", parameter.Name, parameter.ParameterType));
		}

		private bool FindResolves(Type types,string parameterId, Type injecteeType,ICollection<Resolve> outResolves)
		{
			bool foundResolves = false;
			foreach (var resolve in resolves)
			{
				if (ValidResolve(resolve, types, parameterId, injecteeType))
				{
					outResolves.Add(resolve);
					foundResolves = true;
				}
			}
			return foundResolves;
		}

		private bool FindResolve(ParameterInfo parameter, Type injecteeType,out Resolve resolveOut)
		{
			foreach (var resolve in resolves)
			{
				if (ValidResolve(resolve, parameter, injecteeType))
				{
					resolveOut = resolve;
					return true;
				}
			}
			resolveOut = null;
			return false;
		}

		private bool ValidResolve(Resolve resolve, ParameterInfo parameter, Type injecteeType)
		{
			return ValidResolve(resolve,parameter.ParameterType,parameter.Name,injecteeType);
		}

		private bool ValidResolve(Resolve resolve, Type parameterType,string parameterId, Type injecteeType)
		{
			if (resolve.Type != parameterType) return false;
			if (resolve.WhenInjectedIntoType != null && resolve.WhenInjectedIntoType != injecteeType) return false;
			if (!string.IsNullOrEmpty(resolve.id) && parameterId != resolve.id) return false;
			return true;
		}

		public T GetInstance<T>()
		{
			HashSet<Resolve> resolveCallList = new HashSet<Resolve>();
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
				{
					resolveCallList.Add(resolve);
					return (T) resolve.GetInstance();
				}
			}
			return default(T);
		}

		public List<T> GetInstances<T>()
		{
			HashSet<Resolve> resolveCallList = new HashSet<Resolve>();
			List<T> instances = new List<T>();
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
				{
					resolveCallList.Add(resolve);
					instances.Add((T)resolve.GetInstance());
				}
			}
			return instances;
		}

		public class Resolve<T> : Resolve
		{
			public Resolve(InjectionHelper injectionHelper) : base(injectionHelper,typeof(T))
			{
				
			}

			public new T GetInstance()
			{
				return (T)base.GetInstance();
			}

			public Resolve<T> FromMethod(Func<T> method)
			{
				base.FromMethod(()=> method.Invoke());
				return this;
			}

			public new Resolve<T> To(Type type)
			{
				base.To(type);
				return this;
			}

			public new Resolve<T> To<TK>() where TK : T
			{
				base.To(typeof(TK));
				return this;
			}

			public new Resolve<T> WhenInjectedInto(Type type)
			{
				base.WhenInjectedInto(type);
				return this;
			}

			public new Resolve<T> WithId(string id)
			{
				base.WithId(id);
				return this;
			}
		}

		public class Resolve : IEquatable<Resolve>
		{
			private Type type;
			private Type instanceType;
			private Type whenInjectedInto;
			private Func<object> method;
			internal KeyValuePair<string, Type>[] injectedParamsCached;
			internal string id;
			private object instance;
			private readonly InjectionHelper injectionHelper;

			public Resolve(InjectionHelper injectionHelper,Type type)
			{
				this.injectionHelper = injectionHelper;
				this.type = type;
				instanceType = type;
			}

			public bool Equals(Resolve other)
			{
				if (other == null) return false;
				if (type != other.type || whenInjectedInto != other.whenInjectedInto || other.id != id) return false;
				return false;
			}

			public Resolve To(Type type)
			{
				if (this.type.IsAssignableFrom(type) || type == this.type)
				{
					instanceType = type;
				}
				else
				{
					throw new Exception(string.Format("Type '{0}' must be able to be assigned from type '{1}'", type, this.type));
				}
				return this;
			}

			public Resolve To<T>()
			{
				return To(typeof(T));
			}

			public Resolve WhenInjectedInto(Type type)
			{
				whenInjectedInto = type;
				return this;
			}

			public Resolve WhenInjectedInto<T>()
			{
				return WhenInjectedInto(typeof(T));
			}

			public Resolve FromMethod(Func<object> method)
			{
				this.method = method;
				return this;
			}

			public Resolve FromInstance(object instance)
			{
				if (instance == null)
				{
					throw new Exception("Instance cannot be null");
				}
				this.instance = instance;
				if (!type.IsInstanceOfType(instance))
				{
					throw new Exception(string.Format("Instance of type: {0} does not match resolve type: {1}",instance.GetType(),type));
				}
				return this;
			}

			public Resolve WithId(string id)
			{
				this.id = id;
				return this;
			}

			public object GetInstance()
			{
				if (instance == null)
				{
					if (method != null)
						instance = method.Invoke();
					else
						instance = injectionHelper.CreateInstance(instanceType);
				}
				return instance;
			}

			public bool HasInstance { get { return instance != null; } }

			public Type Type
			{
				get { return type; }
			}

			public Type InstanceType
			{
				get { return instanceType; }
			}

			public Type WhenInjectedIntoType
			{
				get { return whenInjectedInto; }
			}

			public string Id
			{
				get { return id; }
			}
		}
	}
}