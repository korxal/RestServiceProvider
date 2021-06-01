using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Runtime.Loader;
using System.Reflection;
using System.Text;
using System.Linq;
using System.IO;
using System;

namespace RestServiceProvider
{


    #region Description
    /*
    This file contains a Wrapper code generator.
    It will generate a wrapper code at runtime, compile it and return the instance of wrapper class
    The main idea behind wrapping is to make any method Rest-compatible

    Example: Method  'string Greet(string name) => "Hello, " + name;'
             Will be wrapped to 'string Greet(Dictionary<string, string> params, string body)'
             Parameters can be taken from the rest method call parameters, body from POST body
             Wrapper code will extract parameters from a dictionary and call wrapped method.
             Output class will be serealized to JSON
    
    Example: call 'Greet("Artemy")' will transfrom to 'Greet(new Dictionary<string,string>(){{"name","Artemy"}},null)'
     */
    #endregion


    public static class DictHelper
    {
        public static T GetValueOrDefault<T>(this Dictionary<string, T> dict, string key)
        {
            if (dict.ContainsKey(key)) return dict[key];
            return default(T);
        }
    }


    public class CodeGen
    {

        private readonly List<string> MethodNames = new List<string>();

        /// <summary>
        /// Generated method list
        /// </summary>
        public List<string> GetMethods => MethodNames;

        /// <summary>
        /// This method wraps object O to Rest-compatible api
        /// The resulting object will contain all methods of O but input parameters will be rest-compatible and result will be wrapped in JSON
        /// </summary>
        /// <param name="O"></param>
        /// <returns></returns>
        public object BuildServiceProviderInstance(object O)
        {
            var SourceType = O.GetType();
            var Code = BuildServiceCode(SourceType);
            var Asm = CompileRestServiceSources(Code, SourceType);
            return Activator.CreateInstance(Asm.ExportedTypes.First(), new object[] { O });
        }


        private static string ConvertSelect(Type t)
        {
            switch (t.Name)
            {
                case "Int32":
                    return "ToInt32";
                case "Int64":
                    return "ToInt64";
                case "decimal":
                case "Decimal":
                    return "ToDecimal";
                case "String":
                case "string":
                    return "ToString";
                case "DateTime":
                    return "ToDateTime";
                default:
                    return $"JsonConvert.DerializeObject<{t.Name}>";
            }

        }

        /// <summary>
        /// This method generates source code to wrap all method in Type T to Rest-compatible ones
        /// Wrapped method should return Json-serialized result of wrapped method. 
        /// 
        /// Namespace name will be 'T.Name + "RestService"'
        /// Class name will be 'T.Name + "ServiceProvider"'
        /// Ex: 'Api' class wrapper will look like ApiRestService.ApiServiceProvider
        /// 
        /// 
        /// </summary>
        /// <param name="T">Type to wrap</param>
        /// <returns>.net source code</returns>
        public string BuildServiceCode(Type T)
        {
            #region Usings
            var sf = SyntaxFactory.CompilationUnit();
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Newtonsoft.Json")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")));
            sf = sf.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(T.Namespace)));


            var ns = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(T.Name + "RestService")).NormalizeWhitespace();
            var cd = SyntaxFactory.ClassDeclaration(T.Name + "ServiceProvider");//Class declaration
            cd = cd.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            #endregion



            #region Constructor
            var ctorParams = SyntaxFactory
               .Parameter(SyntaxFactory.Identifier("o"))
               .WithType(SyntaxFactory.IdentifierName("object"));
            var ctorParamList = SyntaxFactory.SeparatedList<ParameterSyntax>();
            ctorParamList = ctorParamList.Add(ctorParams);

            List<StatementSyntax> ctorStatements = new List<StatementSyntax>();
            ctorStatements.Add(SyntaxFactory.ParseStatement($"c=({T.Namespace}.{T.Name})o;"));
            var ctorBody = SyntaxFactory.Block(ctorStatements);

            var ctor = SyntaxFactory.ConstructorDeclaration(T.Name + "ServiceProvider")
                .WithParameterList(SyntaxFactory.ParameterList(ctorParamList))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(ctorBody);

            cd = cd.AddMembers(ctor);

            #endregion

            #region Fields
            var ci = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(T.Namespace + '.' + T.Name)) //Class Instance
                .AddVariables(SyntaxFactory.VariableDeclarator("c"));
            var fi = SyntaxFactory.FieldDeclaration(ci)
             .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            cd = cd.AddMembers(fi);
            #endregion


            #region Methods


            //Map all  mehods to their methods in original class
            foreach (string MethodName in T.GetMethods().Select(x => x.Name).Distinct())
            {
                // Method may be overloaded, so we select all methods by name
                // exclude  ToString, GetHashCode by module.
                var Methods = T.GetMethods().Where(x => x.Name == MethodName && x.Module == T.Module).ToList();
                if (Methods.Count == 0) continue;
                var methodSource = CreateMethodSource(Methods);
                cd = cd.AddMembers(methodSource);
                MethodNames.Add(MethodName);
            }
            #endregion

            //Wrap up
            ns = ns.AddMembers(cd);
            sf = sf.AddMembers(ns);
            var code = sf
               .NormalizeWhitespace()
               .ToFullString();
            return code;


        }




        /// <summary>
        /// This metod generates a wrapper for original anonymous method. Final code looks like:
        /// 
        ///   public string METHODNAME(Dictionary<string, string> parameters, string body)
        ///   {
        ///      return JsonConvert.SerializeObject(OriginalInstance.METHODNAME(Convert.ToTYPE(parameters.GetValueOrDefault("PARAM1"))));
        ///   }
        /// 
        ///    where METHODNAME - name of method from OriginalInstance
        ///          PARRAM1 - parameter of wrapped method
        ///          TYPE - type of paramerter from wrapped method
        /// 
        /// This wrapper finds a correponding method parameters in url and passes it to a wrapped method
        /// then calls wrapped method and serializes the result
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public MethodDeclarationSyntax CreateMethodSource(List<MethodInfo> Methods)
        {
            string name = Methods[0].Name;
            List<StatementSyntax> MethodStatements = new List<StatementSyntax>();

            //I assume that method may be simple or overloaded.
            //I assume that post request has NO parameters in URL, only serialized json data in body 
            //If method is overloaded then I assume that only two overloads exist and at least one of them has a single parameter, and this parameter is custom class.
            //Assumptions above were made to satisfy mapping of GET and POST requests.

            #region MethodBody
            StringBuilder sb = new StringBuilder();
            MethodInfo m1 = null;
            MethodInfo m2 = null;

            if (Methods.Count > 2) throw new Exception("Ambigious method definition, max 2 overloads currently supported - " + Methods[0].Name);
            bool overload = Methods.Count > 1;
            if (Methods.Count == 1)
            {
                m1 = Methods[0];
            }
            else
            {
                //Try to find method with single parameter. Parameter must be a class from same module.
                m2 = Methods.FirstOrDefault(x => x.GetParameters().Count() == 1 && x.GetParameters()[0].ParameterType.Module == Methods[0].Module);
                if (m2 == null) throw new Exception($"Ambigious method definition '{Methods[0].Name}': Unable to find method that fits POST request. Method must have single parameter. Parameter must be a class from same module");
                m1 = Methods.FirstOrDefault(x => x != m2);
            }


            if (overload)
            {
                sb.Append("if (!String.IsNullOrEmpty(body))");
                if (m2.ReturnType.Name == "Void")
                    sb.Append("c.");
                else
                    sb.Append(" return JsonConvert.SerializeObject(c.");
                sb.Append(name);
                sb.Append("(JsonConvert.DeserializeObject<");
                sb.Append(m2.GetParameters().First().ParameterType.Name);
                sb.Append(">(body))); else");
            }

            if (m1.ReturnType.Name == "Void")
                sb.Append("c.");
            else
                sb.Append(" return JsonConvert.SerializeObject(c.");
            sb.Append(name);
            sb.Append("(");
            foreach (var p in m1.GetParameters())
                sb.Append($"Convert.{ConvertSelect(p.ParameterType)}(parameters.GetValueOrDefault(\"{p.Name}\")),");
            if (m1.GetParameters().Length > 0) sb.Length--;//Chew last comma
            sb.Append("));");

            if (m1.ReturnType.Name == "Void") sb.Append("return \"Call success, no return value\";");

            MethodStatements.Add(SyntaxFactory.ParseStatement(sb.ToString()));
            var MethodBody = SyntaxFactory.Block(MethodStatements);
            #endregion

            #region MethodParameters

            //Dictionnary<string,string>
            var dictType = SyntaxFactory.TypeArgumentList()
                .AddArguments(SyntaxFactory.IdentifierName("string"))
                .AddArguments(SyntaxFactory.IdentifierName("string"));

            var methodParams = SyntaxFactory
                .Parameter(SyntaxFactory.Identifier("parameters"))
                .WithType(SyntaxFactory.GenericName("Dictionary")
                .WithTypeArgumentList(dictType));


            //string body
            var body = SyntaxFactory.Parameter(SyntaxFactory.Identifier("body")).WithType(SyntaxFactory.IdentifierName("string"));

            //wrap up params
            var paramList = SyntaxFactory.SeparatedList<ParameterSyntax>().Add(methodParams).Add(body);
            var MethodParams = SyntaxFactory.ParameterList(paramList);
            #endregion

            var MethodReturn = SyntaxFactory.IdentifierName("string");

            var methodDeclaration = SyntaxFactory.MethodDeclaration(MethodReturn, name)
                                   .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                   .WithParameterList(MethodParams)
                                   .WithBody(MethodBody);
            return methodDeclaration;
        }


        /// <summary>
        /// This method compiles Service code and retuns ready-to-use assembly
        /// </summary>
        /// <param name="Source">Source code to compile</param>
        /// <param name="TypeToWrap">Type to be wrapped</param>
        /// <returns></returns>
        public static Assembly CompileRestServiceSources(string Source, Type TypeToWrap)
        {

#if DEBUG
            //Write generated code to file
            var home = AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(home + "\\src")) Directory.CreateDirectory(home + "\\src");
            File.WriteAllText(home + "src\\" + TypeToWrap.Name + "Source.cs", Source);
#endif
            SyntaxTree ServiceSyntaxTree = CSharpSyntaxTree.ParseText(Source);

            //References to include in our new assembly
            List<MetadataReference> references = new List<MetadataReference>();
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Core").Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Newtonsoft.Json.JsonConvert).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
            references.Add(MetadataReference.CreateFromFile(typeof(string).GetTypeInfo().Assembly.Location));


            //Adds reference to our original class from which we create service
            references.Add(MetadataReference.CreateFromFile(TypeToWrap.Assembly.Location));

            foreach (var reference in TypeToWrap.Assembly.GetReferencedAssemblies())
            {
                var referencedAsm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == reference.Name);
                if (referencedAsm != null && references.FirstOrDefault(x => x.Display == referencedAsm.Location) == null)
                    references.Add(MetadataReference.CreateFromFile(referencedAsm.Location));

            }



            //Prepare to compile
            CSharpCompilation compilation = CSharpCompilation.Create(
            TypeToWrap + "Service", //Assembly Name
            syntaxTrees: new[] { ServiceSyntaxTree }, //Sources to include 
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            //Stream will contain binary IL code
            using (var ms = new MemoryStream())
            {
                EmitResult compiled = compilation.Emit(ms); //This is where compilation happens
                if (!compiled.Success)
                {
                    //Comile error
                    IEnumerable<Diagnostic> failures = compiled.Diagnostics.Where(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error);
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Failed to compile service code:\r\n");
                    foreach (Diagnostic diagnostic in failures)
                    {
                        sb.Append(diagnostic.Location + ":" + diagnostic.GetMessage());
                        sb.Append("\r\n");
                    }
                    throw new Exception(sb.ToString());//Bad case
                }
                else
                {
                    //Compile success
                    ms.Seek(0, SeekOrigin.Begin);//Reset stream position to beginnig
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);//Load new assembly to memory
                    return assembly;
                }
            }
        }


    }
}
