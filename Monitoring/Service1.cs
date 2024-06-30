using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }


        //void LogWriteLine(string message)
        //{
        //    string name = Logindex.ToString("0000");
        //    string logDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log";
        //    FileInfo fileInfo = new FileInfo(logDir + $"\\{name}-log.txt");
        //    using (StreamWriter sw = fileInfo.AppendText())
        //    {
        //        sw.WriteLine(String.Format("{0:yyMMdd hh:mm:ss} - {1}", DateTime.Now.ToString(), message));
        //        sw.Close();
        //        if (fileInfo.Length > 20480)
        //        {
        //            Logindex++;
        //        }

        //        string[] delTimefiles = Directory.GetFiles(logDir, "*", SearchOption.AllDirectories);
        //        foreach (string delTimefile in delTimefiles)
        //        {
        //            FileInfo fi = new FileInfo(delTimefile);
        //            if (fi.CreationTime < DateTime.Now.AddDays(-storageDays)) { fi.Delete(); }
        //        }
        //    }
        //}









        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
