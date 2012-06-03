﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.SharpDevelop.Dom;

namespace ICSharpCode.PackageManagement.EnvDTE
{
	public class CodeType : CodeElement
	{
		/// <summary>
		/// Note that projectContent may be different to the IClass.ProjectContent since the class
		/// is retrieved from the namespace contents and could belong to a separate project or
		/// referenced assembly.
		/// </summary>
		public CodeType(IProjectContent projectContent, IClass c)
			: base(c)
		{
			this.Class = c;
			this.ProjectContent = projectContent;
			InfoLocation = GetInfoLocation(projectContent, c);
		}
		
		vsCMInfoLocation GetInfoLocation(IProjectContent projectContent, IClass c)
		{
			if (projectContent.Project == c.ProjectContent.Project) {
				return vsCMInfoLocation.vsCMInfoLocationProject;
			}
			return vsCMInfoLocation.vsCMInfoLocationExternal;
		}
		
		public CodeType()
		{
		}
		
		protected IClass Class { get; private set; }
		protected IProjectContent ProjectContent { get; private set; }
		
		public virtual vsCMAccess Access {
			get { return GetAccess(); }
			set { }
		}
		
		public virtual string FullName {
			get { return Class.FullyQualifiedName; }
		}
		
		public virtual CodeElements Members {
			get { return new CodeTypeMembers(ProjectContent, Class); }
		}
		
		public virtual CodeElements Bases {
			get { return new CodeTypeBaseTypes(ProjectContent, Class); }
		}
		
		public virtual CodeElements Attributes {
			get { return new CodeAttributes(Class); }
		}
	}
}