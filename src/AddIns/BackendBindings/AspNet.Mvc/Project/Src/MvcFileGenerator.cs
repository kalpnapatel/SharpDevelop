﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.CodeDom.Compiler;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.AspNet.Mvc
{
	public abstract class MvcFileGenerator
	{
		IMvcTextTemplateHostFactory hostFactory;
		
		public MvcFileGenerator(IMvcTextTemplateHostFactory hostFactory)
		{
			this.hostFactory = hostFactory;
		}
		
		public MvcTextTemplateLanguage Language { get; set; }
		public IProject Project { get; set; }
		
		public void GenerateFile(MvcFileName fileName)
		{
			using (IMvcTextTemplateHost host = CreateHost()) {
				GenerateFile(host, fileName);
			}
		} 
		
		IMvcTextTemplateHost CreateHost()
		{
			return hostFactory.CreateMvcTextTemplateHost(Project);
		}
		
		void GenerateFile(IMvcTextTemplateHost host, MvcFileName fileName)
		{
			ConfigureHost(host, fileName);
			string templateFileName = GetTextTemplateFileName();
			string outputViewFileName = fileName.GetPath();
			host.ProcessTemplate(templateFileName, outputViewFileName);
			
			if (host.Errors.Count > 0) {
				CompilerError error = host.Errors[0];
				Console.WriteLine("ProcessTemplate error: " + error.ErrorText);
				Console.WriteLine("ProcessTemplate error: Line: " + error.Line);
			}
		}
		
		protected abstract void ConfigureHost(IMvcTextTemplateHost host, MvcFileName fileName);
		protected abstract string GetTextTemplateFileName();
	}
}