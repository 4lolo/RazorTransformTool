using System;
using System.Globalization;
using System.IO;
using System.Text;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;
using RazorTransformLibary.Data;
using RazorTransformLibary.Utils;

namespace RazorTransformLibary.Generator
{
    public class RazorGenerator : IBaseGenerator
    {
        private string headerString =
            "//------------------------------------------------------------------------------\r\n"
            + "// <auto-generated>\r\n"
            + "//     This code was generated from a template.\r\n"
            + "//\r\n"
            + "//     Manual changes to this file may cause unexpected behavior in your application.\r\n"
            + "//     Manual changes to this file will be overwritten if the code is regenerated.\r\n"
            + "// </auto-generated>\r\n"
            + "//------------------------------------------------------------------------------\r\n";
        private string _filePath;
        private object _modeldata;
        private ITemplateSource _templateSource;
        private ITemplateKey _templateKey;
        private IRazorEngineService _engine;
        public RazorFileTemplate RazorTemplate { get; private set; }
        private string _result;
        private string _fileContent;
        private DynamicViewBag _viewBag;
        public RazorGenerator()
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath">path of file template</param>
        /// <param name="templateContent">template content(if it is null engine will read data in filepath</param>
        /// <param name="modeldata"></param>
        /// 
        public RazorGenerator(string filePath, string templateContent, object modeldata)
        {
            _filePath = _fileContent = filePath;
            _fileContent = templateContent;
            _modeldata = modeldata;
        }

        public void Init()
        {

            //load file setting with config in header
            RazorTemplate = FileTemplateManager.LoadFileTemplate(_filePath, _fileContent);
            //create config for razor engine
            //http://antaris.github.io/RazorEngine/
            var config = new TemplateServiceConfiguration();
            config.EncodedStringFactory = new RawStringFactory();
            config.BaseTemplateType = typeof(CustomTemplateBase<>);
            config.TemplateManager = new DelegateTemplateManager();
            var referenceResolver = new MyIReferenceResolver();
            referenceResolver.DllFolder = RazorTemplate.InputDllFolder;
            config.ReferenceResolver = referenceResolver;
            _engine = RazorEngineService.Create(config);
            _templateKey = Engine.Razor.GetKey(MyTemplateKey.MAIN_TEMPLATE);
            //setup viewbag input Folder for render partial
            _viewBag = new DynamicViewBag();
            _viewBag.AddValue("InputFolder", RazorTemplate.InputFolder);
            //check template is compile
            if (!_engine.IsTemplateCached(_templateKey, null))
            {
                //add include template file
                var includeTemplate = new StringBuilder();
                foreach (var filepath in RazorTemplate.ListImportFile)
                {
                    includeTemplate.Append(FileUtils.ReadFileContent(filepath));
                }
                var data = RazorTemplate.TemplateData;
                data = includeTemplate.ToString() + data;
                _templateSource = new LoadedTemplateSource(data);
                _engine.AddTemplate(_templateKey, _templateSource);
                _engine.Compile(_templateKey, _modeldata.GetType());
            }
        }

        public string Render()
        {
            _result = _engine.Run(_templateKey, _modeldata.GetType(), _modeldata, _viewBag).Trim();
            //put header for result code
            if (RazorTemplate.IsHeader)
            {
                _result = headerString + _result;
            }
            return _result;
        }

        public void OutPut()
        {
            try
            {
                if (!string.IsNullOrEmpty(RazorTemplate.OutPutFile))
                {
                    FileUtils.WriteToFile(RazorTemplate.OutPutFile, _result.Trim());
                }
            }
            catch (Exception)
            {

                throw new Exception("Can't write file with path +" + RazorTemplate.OutPutFile);
            }

        }
    }
    [Serializable]
    public class CustomTemplateBase<X> : TemplateBase<X>
    {
        private string _tabString = "\t";
        //private IRazorEngineService _engine;
        //private RazorFileTemplate _razorFileTemplate;
        //public CustomTemplateBase(IRazorEngineService engine, RazorFileTemplate razorFileTemplate) : base()
        //{
        //    _engine = engine;
        //    _razorFileTemplate = razorFileTemplate;
        //}

        public string Partial(string partialName, object obj)
        {
            var path = partialName;
            if (this.ViewBag.InputFolder != null)
            {
                path = FileUtils.GetPartialPath(this.ViewBag.InputFolder, path);
            }
            if (File.Exists(path))
            {
                try
                {
                    //compile partial template
                    var fileContent = FileUtils.ReadFileContent(path);
                    var templateKey=Razor.GetKey(partialName);
                    if (!Razor.IsTemplateCached(templateKey, null))
                    {
                        var templateSource = new LoadedTemplateSource(fileContent);
                        Razor.AddTemplate(templateKey, templateSource);
                        Razor.Compile(templateKey, obj.GetType());
                    }
                    Include(partialName, obj, null).WriteTo(this.CurrentWriter);
                    return string.Empty;
                }
                catch (TemplateCompilationException tex)
                {
                    return string.Format("Partial Render Error{0}\r\n{1}", path, MRazorUtil.GetError(tex));
                }
                catch (Exception ex)
                {
                    return string.Format("Partial Render Error{0}\r\n{1}", path, ex.Message);
                }
            }
            else
            {
                return "Partial file Not Found " + path;
            }
        }

        /// <summary>
        /// Simple write @ for .cshtml file
        /// </summary>
        /// <returns></returns>
        public string R2()
        {
            return "@";
        }
        /// <summary>
        /// Write Raw String file
        /// </summary>
        /// <param name="str">Raw</param>
        /// <returns></returns>
        public string R(string str)
        {
            return str;
        }
        public string NL
        {
            get { return Environment.NewLine; }
        }

        public string T
        {
            get
            {
                return _tabString;
            }
        }
        /// <summary>
        /// Write line with string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string WF(string str)
        {
            return WF(0, str);
        }
        /// <summary>
        /// Write line and add some \t in start of line
        /// </summary>
        /// <param name="numTab">number of \t added</param>
        /// <param name="data">line data</param>
        /// <returns></returns>
        public string WF(int numTab, string data = default(string))
        {
            string result = "";
            for (var i = 0; i < numTab; i++)
            {
                result += _tabString;
            }
            return result + data + Environment.NewLine;
        }

        public string WF(string str, params string[] value)
        {
            return WF(0, str, value);
        }

        public string WF(int num, string str, params string[] value)
        {
            try
            {
                string result = "";
                for (var i = 0; i < num; i++)
                {
                    result += _tabString;
                }
                return result + string.Format(str, value) + Environment.NewLine;
            }
            catch (Exception)
            {
                throw new Exception(string.Format("params string input not engough : [ {0} ]", str));
            }
        }

        public string ToTitleCase(string str)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        }
    }
}
