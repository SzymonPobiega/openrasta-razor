using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Web.Razor;
using System.Web.Razor.Generator;
using System.Web.Razor.Parser;
using System.Web.WebPages;

namespace OpenRasta.Codecs.Razor
{
    public class OpenRastaRazorHost : RazorEngineHost
    {
        internal const string ApplicationInstancePropertyName = "ApplicationInstance";
        internal const string ContextPropertyName = "Context";
        internal const string DefineSectionMethodName = "DefineSection";
        internal const string WebDefaultNamespace = "ASP";
        internal const string WriteToMethodName = "WriteTo";
        internal const string WriteLiteralToMethodName = "WriteLiteralTo";


        internal static readonly string PageBaseClass = typeof(RazorViewBase).FullName;
        internal static readonly string TemplateTypeName = typeof(HelperResult).FullName;

        private static readonly ConcurrentDictionary<string, object> _importedNamespaces = new ConcurrentDictionary<string, object>();

        private string _className;
        private RazorCodeLanguage _codeLanguage;
        private string _physicalPath = null;

        public override RazorCodeLanguage CodeLanguage
        {
            get
            {
                if (_codeLanguage == null)
                {
                    _codeLanguage = GetCodeLanguage();
                }
                return _codeLanguage;
            }
            protected set { _codeLanguage = value; }
        }        

        public override string DefaultClassName
        {
            get
            {
                if (_className == null)
                {
                    _className = GetClassName(VirtualPath);
                }
                return _className;
            }
            set { _className = value; }
        }

        public bool DefaultDebugCompilation { get; set; }

        public string DefaultPageBaseClass { get; set; }        
       
        public string PhysicalPath
        {
            get
            {
                MapPhysicalPath();
                return _physicalPath;
            }
            set { _physicalPath = value; }
        }
        
        public string VirtualPath { get; private set; }

        private OpenRastaRazorHost()
        {
            NamespaceImports.Add("System");
            NamespaceImports.Add("System.Collections.Generic");
            NamespaceImports.Add("System.IO");
            NamespaceImports.Add("System.Linq");
            NamespaceImports.Add("System.Net");
            NamespaceImports.Add("System.Web");
            //NamespaceImports.Add("System.Web.Helpers");
            NamespaceImports.Add("System.Web.Security");
            NamespaceImports.Add("System.Web.UI");
            NamespaceImports.Add("System.Web.WebPages");
            
            DefaultNamespace = WebDefaultNamespace;
            GeneratedClassContext = new GeneratedClassContext(GeneratedClassContext.DefaultExecuteMethodName,
                                                              GeneratedClassContext.DefaultWriteMethodName,
                                                              GeneratedClassContext.DefaultWriteLiteralMethodName,
                                                              WriteToMethodName,
                                                              WriteLiteralToMethodName,
                                                              TemplateTypeName,
                                                              DefineSectionMethodName);
            DefaultPageBaseClass = PageBaseClass;
            DefaultBaseClass = typeof (RazorViewBase<>).AssemblyQualifiedName;
            DefaultDebugCompilation = true;
        }

        public OpenRastaRazorHost(string virtualPath)
            : this(virtualPath, null)
        {
        }

        public OpenRastaRazorHost(string virtualPath, string physicalPath)
            : this()
        {
            if (String.IsNullOrEmpty(virtualPath)) { throw new ArgumentException("Argument cannot be null or an empty string.", "virtualPath"); }

            VirtualPath = virtualPath;

            PhysicalPath = physicalPath;
            DefaultClassName = GetClassName(VirtualPath);
            CodeLanguage = GetCodeLanguage();
        }

        public static void AddGlobalImport(string ns)
        {
            if (String.IsNullOrEmpty(ns)) { throw new ArgumentException("Argument cannot be null or an empty string.", "ns"); }

            _importedNamespaces.TryAdd(ns, null);
        }

        public override RazorCodeGenerator DecorateCodeGenerator(RazorCodeGenerator incomingCodeGenerator)
        {
            if (incomingCodeGenerator is CSharpRazorCodeGenerator)
            {
                return new OpenRastaCSharpRazorCodeGenerator(incomingCodeGenerator.ClassName,
                                                       incomingCodeGenerator.RootNamespaceName,
                                                       incomingCodeGenerator.SourceFileName,
                                                       incomingCodeGenerator.Host);
            }
            if (incomingCodeGenerator is VBRazorCodeGenerator)
            {
                throw new InvalidOperationException("VB not supported yet.");
            }
            return base.DecorateCodeGenerator(incomingCodeGenerator);
        }

        public override ParserBase DecorateCodeParser(ParserBase incomingCodeParser)
        {
            if (incomingCodeParser is CSharpCodeParser)
            {
                return new OpenRastaCSharpRazorCodeParser();
            }
            if (incomingCodeParser is VBCodeParser)
            {
                throw new InvalidOperationException("VB not supported yet.");
            }
            return base.DecorateCodeParser(incomingCodeParser);
        }

        public override MarkupParser CreateMarkupParser()
        {
            return new HtmlMarkupParser();
        }

        private static RazorCodeLanguage DetermineCodeLanguage(string fileName)
        {
            string extension = Path.GetExtension(fileName);

            // Use an if rather than else-if just in case Path.GetExtension returns null for some reason
            if (String.IsNullOrEmpty(extension))
            {
                return null;
            }
            if (extension[0] == '.')
            {
                extension = extension.Substring(1); // Trim off the dot
            }

            // Look up the language
            // At the moment this only deals with code languages: cs, vb, etc., but in theory we could have MarkupLanguageServices which allow for
            // interesting combinations like: vbcss, csxml, etc.
            RazorCodeLanguage language = GetLanguageByExtension(extension);
            return language;
        }

        protected virtual string GetClassName(string virtualPath)
        {
            return ParserHelpers.SanitizeClassName(Path.GetFileName(virtualPath));
        }

        protected virtual RazorCodeLanguage GetCodeLanguage()
        {
            RazorCodeLanguage language = DetermineCodeLanguage(VirtualPath);
            if (language == null && !String.IsNullOrEmpty(PhysicalPath))
            {
                language = DetermineCodeLanguage(PhysicalPath);
            }

            if (language == null)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Could not determine the code language for '{0}'", VirtualPath));
            }

            return language;
        }

        public static IEnumerable<string> GetGlobalImports()
        {
            return _importedNamespaces.ToArray().Select(pair => pair.Key);
        }

        private static RazorCodeLanguage GetLanguageByExtension(string extension)
        {
            return RazorCodeLanguage.GetLanguageByExtension(extension);
        }

        private void MapPhysicalPath()
        {
            if (_physicalPath == null && HostingEnvironment.IsHosted)
            {
                string path = HostingEnvironment.MapPath(VirtualPath);
                if (!String.IsNullOrEmpty(path) && File.Exists(path))
                {
                    _physicalPath = path;
                }
            }
        }

        public override void PostProcessGeneratedCode(CodeCompileUnit codeCompileUnit,
                                                      CodeNamespace generatedNamespace,
                                                      CodeTypeDeclaration generatedClass,
                                                      CodeMemberMethod executeMethod)
        {
            base.PostProcessGeneratedCode(codeCompileUnit, generatedNamespace, generatedClass, executeMethod);
            generatedNamespace.Imports.AddRange(GetGlobalImports().Select(s => new CodeNamespaceImport(s)).ToArray());            
        }        
    }
}