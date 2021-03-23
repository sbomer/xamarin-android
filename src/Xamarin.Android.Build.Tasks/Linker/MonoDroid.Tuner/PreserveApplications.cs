using System;
using System.Collections;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class PreserveApplications
#if NET5_LINKER
	: IMarkHandler
#else
	: BaseSubStep
#endif
	{

#if NET5_LINKER
		LinkContext context;
		AnnotationStore Annotations => context?.Annotations;

		public void Initialize (LinkContext context, MarkContext markContext)
		{
			this.context = context;
			markContext.RegisterMarkAssemblyAction (assembly => ProcessAssembly (assembly));
			markContext.RegisterMarkTypeAction (type => ProcessType (type));
		}
#else
		public override SubStepTargets Targets {
			get { return SubStepTargets.Type
				| SubStepTargets.Assembly;
			}
		}
#endif

		public
#if !NET5_LINKER
		override
#endif
		bool IsActiveFor (AssemblyDefinition assembly)
		{
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public 
#if !NET5_LINKER
		override
#endif
		void ProcessAssembly (AssemblyDefinition assembly)
		{
#if NET5_LINKER
			if (!IsActiveFor (assembly))
				return;
#endif
			ProcessAttributeProvider (assembly);
		}

		public
#if !NET5_LINKER
		override
#endif
		void ProcessType (TypeDefinition type)
		{
#if NET5_LINKER
			if (!IsActiveFor (type.Module.Assembly))
				return;
#endif
			if (!type.Inherits ("Android.App.Application"))
				return;

			ProcessAttributeProvider (type);
		}

		void ProcessAttributeProvider (ICustomAttributeProvider provider)
		{
			if (!provider.HasCustomAttributes)
				return;

			const string ApplicationAttribute = "Android.App.ApplicationAttribute";

			foreach (CustomAttribute attribute in provider.CustomAttributes)
				if (attribute.Constructor.DeclaringType.FullName == ApplicationAttribute)
					PreserveApplicationAttribute (attribute);
		}

		void PreserveApplicationAttribute (CustomAttribute attribute)
		{
			PreserveTypeProperty (attribute, "BackupAgent");
			PreserveTypeProperty (attribute, "ManageSpaceActivity");
		}

		void PreserveTypeProperty (CustomAttribute attribute, string property)
		{
			if (!attribute.HasProperties)
				return;

			var type_ref = (TypeReference) attribute.Properties.First (p => p.Name == property).Argument.Value;
			if (type_ref == null)
				return;

			var type = type_ref.Resolve ();
			if (type == null)
				return;

			PreserveDefaultConstructor (type);
		}

		void PreserveDefaultConstructor (TypeDefinition type)
		{
			if (!type.HasMethods)
				return;

			foreach (MethodDefinition ctor in type.Methods.Where (t => t.IsConstructor)) {
				if (!ctor.IsStatic && !ctor.HasParameters) {
					PreserveMethod (type, ctor);
					break;
				}
			}
		}

		void PreserveMethod (TypeDefinition type, MethodDefinition method)
		{
			Annotations.AddPreservedMethod (type, method);
		}
	}
}
