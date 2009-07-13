﻿//===============================================================================
// Microsoft patterns & practices
// Unity Application Block
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Practices.ObjectBuilder2.Tests.TestDoubles;
using Microsoft.Practices.ObjectBuilder2.Tests.TestObjects;
using Microsoft.Practices.Unity.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.ObjectBuilder2.Tests
{
    [TestClass]
    public class DynamicMethodPropertySetterFixture
    {
        [TestMethod]
        public void CanInjectProperties()
        {
            MockBuilderContext context = GetContext();
            object existingObject = new object();
            SingletonLifetimePolicy lifetimePolicy = new SingletonLifetimePolicy();
            lifetimePolicy.SetValue(existingObject);
            context.Policies.Set<ILifetimePolicy>(lifetimePolicy, typeof(object));

            IBuildPlanPolicy plan =
                GetPlanCreator(context).CreatePlan(context, typeof(OnePropertyClass));

            OnePropertyClass existing = new OnePropertyClass();
            context.Existing = existing;
            context.BuildKey = typeof(OnePropertyClass);
            plan.BuildUp(context);

            Assert.IsNotNull(existing.Key);
            Assert.AreSame(existingObject, existing.Key);
        }

        [TestMethod]
        public void TheCurrentOperationIsNullAfterSuccessfullyExecutingTheBuildPlan()
        {
            MockBuilderContext context = GetContext();
            context.BuildKey = typeof(OnePropertyClass);
            context.Existing = new OnePropertyClass();

            IBuildPlanPolicy plan = GetPlanCreator(context).CreatePlan(context, typeof(OnePropertyClass));
            plan.BuildUp(context);

            Assert.IsNull(context.CurrentOperation);
        }

        [TestMethod]
        public void ResolvingAPropertyValueSetsTheCurrentOperation()
        {
            var resolverPolicy = new CurrentOperationSensingResolverPolicy<object>();

            MockBuilderContext context = GetContext();
            context.BuildKey = typeof(OnePropertyClass);
            context.Existing = new OnePropertyClass();

            context.Policies.Set<IPropertySelectorPolicy>(
                new TestSinglePropertySelectorPolicy<OnePropertyClass>(resolverPolicy),
                typeof(OnePropertyClass));

            IBuildPlanPolicy plan = GetPlanCreator(context).CreatePlan(context, typeof(OnePropertyClass));
            plan.BuildUp(context);

            Assert.IsNotNull(resolverPolicy.currentOperation);
        }

        [TestMethod]
        public void ExceptionThrownWhileResolvingAPropertyValueIsBubbledUpAndTheCurrentOperationIsNotCleared()
        {
            var exception = new ArgumentException();
            var resolverPolicy = new ExceptionThrowingTestResolverPolicy(exception);

            MockBuilderContext context = GetContext();
            context.BuildKey = typeof(OnePropertyClass);
            context.Existing = new OnePropertyClass();

            context.Policies.Set<IPropertySelectorPolicy>(
                new TestSinglePropertySelectorPolicy<OnePropertyClass>(resolverPolicy),
                typeof(OnePropertyClass));

            IBuildPlanPolicy plan = GetPlanCreator(context).CreatePlan(context, typeof(OnePropertyClass));

            try
            {
                plan.BuildUp(context);
                Assert.Fail("failure expected");
            }
            catch (Exception e)
            {
                Assert.AreSame(exception, e);

                var operation = (ResolvingPropertyValueOperation) context.CurrentOperation;
                Assert.IsNotNull(operation);
                Assert.AreSame(typeof(OnePropertyClass), operation.TypeBeingConstructed);
                Assert.AreEqual("Key", operation.PropertyName);
            }
        }

        [TestMethod]
        public void ExceptionThrownWhileSettingAPropertyIsBubbledUpAndTheCurrentOperationIsNotCleared()
        {
            MockBuilderContext context = GetContext();
            context.BuildKey = typeof(OneExceptionThrowingPropertyClass);
            context.Existing = new OneExceptionThrowingPropertyClass();

            IBuildPlanPolicy plan =
                GetPlanCreator(context).CreatePlan(context, typeof(OneExceptionThrowingPropertyClass));

            try
            {
                plan.BuildUp(context);
                Assert.Fail("failure expected");
            }
            catch (Exception e)
            {
                Assert.AreSame(OneExceptionThrowingPropertyClass.propertySetterException, e);
                var operation = (SettingPropertyOperation) context.CurrentOperation;
                Assert.IsNotNull(operation);

                Assert.AreSame(typeof(OneExceptionThrowingPropertyClass), operation.TypeBeingConstructed);
                Assert.AreEqual("Key", operation.PropertyName);
            }
        }

        private MockBuilderContext GetContext()
        {
            StagedStrategyChain<BuilderStage> chain = new StagedStrategyChain<BuilderStage>();
            chain.AddNew<DynamicMethodPropertySetterStrategy>(BuilderStage.Initialization);

            DynamicMethodBuildPlanCreatorPolicy policy =
                new DynamicMethodBuildPlanCreatorPolicy(chain);

            MockBuilderContext context = new MockBuilderContext();

            context.Strategies.Add(new LifetimeStrategy());

            context.PersistentPolicies.SetDefault<IDynamicBuilderMethodCreatorPolicy>(
                DynamicBuilderMethodCreatorFactory.CreatePolicy());

            context.Policies.SetDefault<IConstructorSelectorPolicy>(
                new ConstructorSelectorPolicy<InjectionConstructorAttribute>());
            context.Policies.SetDefault<IPropertySelectorPolicy>(
                new PropertySelectorPolicy<DependencyAttribute>());
            context.Policies.SetDefault<IBuildPlanCreatorPolicy>(policy);

            return context;
        }

        private IBuildPlanCreatorPolicy GetPlanCreator(IBuilderContext context)
        {
            return context.Policies.Get<IBuildPlanCreatorPolicy>(null);
        }

        public class TestSinglePropertySelectorPolicy<T> : IPropertySelectorPolicy
        {
            private IDependencyResolverPolicy resolverPolicy;

            public TestSinglePropertySelectorPolicy(IDependencyResolverPolicy resolverPolicy)
            {
                this.resolverPolicy = resolverPolicy;
            }

            public IEnumerable<SelectedProperty> SelectProperties(IBuilderContext context)
            {
                var key = Guid.NewGuid().ToString();
                context.Policies.Set<IDependencyResolverPolicy>(this.resolverPolicy, key);
                yield return
                    new SelectedProperty(
                        typeof(T).GetProperties(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)[0],
                        key);
            }
        }

        public class OnePropertyClass
        {
            private object key;

            [Dependency]
            public object Key
            {
                get { return key; }
                set { key = value; }
            }
        }

        public class OneExceptionThrowingPropertyClass
        {
            public static Exception propertySetterException = new ArgumentException();

            [Dependency]
            public object Key
            {
                set { throw propertySetterException; }
            }
        }

        public interface IFoo
        {
        }

        public class ClassThatTakesInterface
        {
            [Dependency]
            public IFoo Foo
            {
                get { return null; }
                set { }
            }
        }
    }
}
