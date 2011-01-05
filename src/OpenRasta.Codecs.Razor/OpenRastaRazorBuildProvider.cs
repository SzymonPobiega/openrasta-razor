using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Web;
using System.Web.Compilation;
using System.Web.Razor;
using System.Web.Razor.Generator;
using System.Web.Razor.Parser.SyntaxTree;

namespace OpenRasta.Codecs.Razor
{
    [BuildProviderAppliesTo(BuildProviderAppliesTo.Web | BuildProviderAppliesTo.Code)]
    public class OpenRastaRazorBuildProvider : BuildProvider
    {
        public static event EventHandler<CodeGenerationCompleteEventArgs> CodeGenerationCompleted;
        public static event EventHandler CodeGenerationStarted;

        // For unit testing
        private event EventHandler<CodeGenerationCompleteEventArgs> _codeGenerationCompletedInternal;
        private event EventHandler _codeGenerationStartedInternal;

        private static bool? _isFullTrust;
        private CodeCompileUnit _generatedCode = null;
        private OpenRastaRazorHost _host = null;
        private IList _virtualPathDependencies;
        private AssemblyBuilder _assemblyBuilder;

        internal OpenRastaRazorHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = CreateHost();
                }
                return _host;
            }
            set { _host = value; }
        }

        // Returns the base dependencies and any dependencies added via AddVirtualPathDependencies
        public override ICollection VirtualPathDependencies
        {
            get
            {
                if (_virtualPathDependencies != null)
                {
                    // Return a readonly wrapper so as to prevent users from modifying the collection directly.
                    return ArrayList.ReadOnly(_virtualPathDependencies);
                }
                else
                {
                    return base.VirtualPathDependencies;
                }
            }
        }

        public void AddVirtualPathDependency(string dependency)
        {
            if (_virtualPathDependencies == null)
            {
                // Initialize the collection containing the base dependencies
                _virtualPathDependencies = new ArrayList(base.VirtualPathDependencies);
            }

            _virtualPathDependencies.Add(dependency);
        }

        public new string VirtualPath
        {
            get
            {
                return base.VirtualPath;
            }
        }

        public AssemblyBuilder AssemblyBuilder
        {
            get
            {
                return _assemblyBuilder;
            }
        }

        internal CodeCompileUnit GeneratedCode
        {
            get
            {
                EnsureGeneratedCode();
                return _generatedCode;
            }
            set { _generatedCode = value; }
        }

        public override CompilerType CodeCompilerType
        {
            get
            {
                EnsureGeneratedCode();
                CompilerType compilerType = GetDefaultCompilerTypeForLanguage(Host.CodeLanguage.LanguageName);
                if (_isFullTrust != false && Host.DefaultDebugCompilation)
                {
                    try
                    {
                        SetIncludeDebugInfoFlag(compilerType);
                        _isFullTrust = true;
                    }
                    catch (SecurityException)
                    {
                        _isFullTrust = false;
                    }
                }
                return compilerType;
            }
        }

        internal event EventHandler<CodeGenerationCompleteEventArgs> CodeGenerationCompletedInternal
        {
            add { _codeGenerationCompletedInternal += value; }
            remove { _codeGenerationCompletedInternal -= value; }
        }

        // For unit testing
        internal event EventHandler CodeGenerationStartedInternal
        {
            add { _codeGenerationStartedInternal += value; }
            remove { _codeGenerationStartedInternal -= value; }
        }

        public override Type GetGeneratedType(CompilerResults results)
        {
            return results.CompiledAssembly.GetType(String.Format(CultureInfo.CurrentCulture, "{0}.{1}", Host.DefaultNamespace, Host.DefaultClassName));
        }

        public override void GenerateCode(AssemblyBuilder assemblyBuilder)
        {
            GenerateCodeCore(assemblyBuilder);
        }

        internal virtual void GenerateCodeCore(AssemblyBuilder assemblyBuilder)
        {
            OnCodeGenerationStarted(assemblyBuilder);
            assemblyBuilder.AddCodeCompileUnit(this, GeneratedCode);
        }

        protected internal virtual TextReader InternalOpenReader()
        {
            return OpenReader();
        }

        protected internal virtual OpenRastaRazorHost CreateHost()
        {
            return OpenRastaRazorHostFactory.CreateHost(VirtualPath);
        }

        private void OnCodeGenerationStarted(AssemblyBuilder assemblyBuilder)
        {
            _assemblyBuilder = assemblyBuilder;
            EventHandler handler = _codeGenerationStartedInternal ?? CodeGenerationStarted;
            if (handler != null)
            {
                handler(this, null);
            }
        }

        private void OnCodeGenerationCompleted(CodeCompileUnit generatedCode)
        {
            EventHandler<CodeGenerationCompleteEventArgs> handler = _codeGenerationCompletedInternal ?? CodeGenerationCompleted;
            if (handler != null)
            {
                handler(this, new CodeGenerationCompleteEventArgs(Host.VirtualPath, Host.PhysicalPath, generatedCode));
            }
        }

        private void EnsureGeneratedCode()
        {
            if (_generatedCode == null)
            {
                var engine = new RazorTemplateEngine(Host);
                GeneratorResults results;
                using (TextReader reader = InternalOpenReader())
                {
                    results = engine.GenerateCode(reader);
                }
                if (!results.Success)
                {
                    throw CreateExceptionFromParserError(results.ParserErrors.Last(), VirtualPath);
                }
                _generatedCode = results.GeneratedCode;

                // Run the code gen complete event
                OnCodeGenerationCompleted(_generatedCode);
            }
        }

        private static HttpParseException CreateExceptionFromParserError(RazorError error, string virtualPath)
        {
            return new HttpParseException(error.Message + Environment.NewLine, null, virtualPath, null, error.Location.LineIndex + 1);
        }

        private static void SetIncludeDebugInfoFlag(CompilerType compilerType)
        {
            compilerType.CompilerParameters.IncludeDebugInformation = true;
        }
    }
}