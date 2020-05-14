// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class BindingsGenerator : AndroidDotnetToolTask
	{
		public override string TaskPrefix => "BGN";

		public bool OnlyRunXmlAdjuster { get; set; }

		public string XmlAdjusterOutput { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public string EnumDirectory { get; set; }

		public string EnumMetadataDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public string ApiXmlInput { get; set; }

		public string AssemblyName { get; set; }
		
		public string CodegenTarget { get; set; }

		public bool NoStdlib { get; set; }

		public string TypeMappingReportFile { get; set; }

		public bool UseShortFileNames { get; set; }

		// apart from ReferencedManagedLibraries we need it to find mscorlib.dll.
		[Required]
		public string MonoAndroidFrameworkDirectories { get; set; }

		public string LangVersion { get; set; }

		public bool EnableInterfaceMembersPreview { get; set; }

		public ITaskItem[] TransformFiles { get; set; }
		public ITaskItem[] ReferencedManagedLibraries { get; set; }
		public ITaskItem[] AnnotationsZipFiles { get; set; }

		private List<Tuple<string, string>> transform_files = new List<Tuple<string,string>> ();

		public override bool RunTask ()
		{
			Directory.CreateDirectory (OutputDirectory);

			// We need to do this validation in Execute rather than GenerateCommandLineCommands
			// because we can't terminate the build nicely in GenerateCommandLineCommands.
			if (TransformFiles != null)
				foreach (var fixup in TransformFiles) {
					try {
						var doc = XDocument.Load (fixup.ItemSpec);

						switch (doc.Root.Name.LocalName) {
							case "metadata":
								Log.LogDebugMessage ("Adding transform file {0} as metadata.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "fixup"));
								break;
							case "enum-field-mappings":
								Log.LogDebugMessage ("Adding transform file {0} as enumfields.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "enumfields"));
								break;
							case "enum-method-mappings":
								Log.LogDebugMessage ("Adding transform file {0} as enummethods.", fixup.ItemSpec);
								transform_files.Add (new Tuple<string, string> (fixup.ItemSpec, "enummethods"));
								break;
							default:
								Log.LogCodedError (
									code: "XA4229",
									file: fixup.ItemSpec,
									lineNumber: 0,
									message: Properties.Resources.XA4229,
									messageArgs: new [] {
										doc.Root.Name.LocalName
									}
								);
								return false;
						}
					} catch (Exception ex) {
						Log.LogCodedError (
							code: "XA4230",
							file: fixup.ItemSpec,
							lineNumber: 0,
							message: Properties.Resources.XA4230,
							messageArgs: new [] {
								ex
							}
						);
						return false;
					}
				}

			return base.RunTask ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = GetCommandLineBuilder ();
			cmd.AppendFileNameIfNotNull (ApiXmlInput);

			if (OnlyRunXmlAdjuster)
				cmd.AppendSwitch ("--only-xml-adjuster");
			cmd.AppendSwitchIfNotNull ("--xml-adjuster-output=", XmlAdjusterOutput);

			cmd.AppendSwitchIfNotNull ("--codegen-target=", CodegenTarget);
			cmd.AppendSwitchIfNotNull ("--csdir=", OutputDirectory);
			cmd.AppendSwitchIfNotNull ("--enumdir=", EnumDirectory);
			cmd.AppendSwitchIfNotNull ("--enummetadata=", EnumMetadataDirectory);
			cmd.AppendSwitchIfNotNull ("--assembly=", AssemblyName);

			if (!NoStdlib) {
				string fxpath = MonoAndroidFrameworkDirectories.Split (';').First (p => new DirectoryInfo (p).GetFiles ("mscorlib.dll").Any ());
				cmd.AppendSwitchIfNotNull ("--ref=", Path.Combine (Path.GetFullPath (fxpath), "mscorlib.dll"));
			}
			
			if (ReferencedManagedLibraries != null)
				foreach (var lib in ReferencedManagedLibraries)
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (lib.ItemSpec));
			if (AnnotationsZipFiles != null)
				foreach (var zip in AnnotationsZipFiles)
					cmd.AppendSwitchIfNotNull ("--annotations=", Path.GetFullPath (zip.ItemSpec));

			foreach (var tf in transform_files)
				cmd.AppendSwitchIfNotNull (string.Format ("--{0}=", tf.Item2), tf.Item1);

			cmd.AppendSwitchIfNotNull ("--api-level=", AndroidApiLevel);

			cmd.AppendSwitchIfNotNull ("--type-map-report=", TypeMappingReportFile);

			cmd.AppendSwitch ("--global");
			cmd.AppendSwitch ("--public");

			if (UseShortFileNames)
				cmd.AppendSwitch ("--use-short-file-names");

			if (EnableInterfaceMembersPreview && SupportsCSharp8)
				cmd.AppendSwitch ("--lang-features=interface-constants,default-interface-methods");

			return cmd.ToString ();
		}

		protected override string BaseToolName => "generator";

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);

			if (messageImportance != StandardErrorLoggingImportance)
				return;

			Log.LogFromStandardError ("BG0000", singleLine);
		}

		bool SupportsCSharp8 {
			get {
				// These are the values that pre-date C# 8.  We assume any
				// new value we encounter is something that supports it.
				switch (LangVersion) {
					case "7.3":
					case "7.2":
					case "7.1":
					case "7":
					case "6":
					case "5":
					case "4":
					case "3":
					case "ISO-2":
					case "ISO-1":
						return false;
				}

				return true;
			}
		}
	}
}
