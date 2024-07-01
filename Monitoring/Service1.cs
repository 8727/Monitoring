using System;
using System.IO;
using System.Configuration;
using System.Collections;
using System.ServiceProcess;
using System.Timers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Web.Script.Serialization;
using System.Net.Http;
using Microsoft.Win32;
using System.Data;
using System.Threading;
using System.Net;
using System.Text;
using System.Net.NetworkInformation;

namespace Monitoring
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        TimeSpan localZone = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

        static HttpListener serverWeb;
        Thread WEBServer = new Thread(ThreadWEBServer);

        class ReplicatorCh
        {
            public string host;
            public string LastReplicationTime;
            public Int64 LastReplicationTimeFt;
            public Int64 OldLastReplicationTimeFt;
            public string LastReplicationLocalTime;
            public string LagReplication;
        }

        static Hashtable Replicator = new Hashtable();
        static Hashtable ViewCamera = new Hashtable();
        Hashtable ViolationCode = new Hashtable();

        void HashVuolation()
        {
            ViolationCode.Add("0", "0 - Stream");
            ViolationCode.Add("2", "2 - OverSpeed");
            ViolationCode.Add("4", "4 - WrongDirection");
            ViolationCode.Add("5", "5 - BusLane");
            ViolationCode.Add("10", "10 - RedLightCross");
            ViolationCode.Add("31", "31 - SeatBelt");
            ViolationCode.Add("81", "81 - WrongCross");
            ViolationCode.Add("83", "83 - StopLine");
            ViolationCode.Add("90", "90 - WrongTurnTwoFrontier");
            ViolationCode.Add("112", "112 - WrongLineTurn");
            ViolationCode.Add("113", "113 - NoForwardZnak");
            ViolationCode.Add("114", "114 - NoUTurnOnlyForward");
            ViolationCode.Add("127", "127 - Lights");
        }

        string sourceFolderPr = "D:\\Duplo";
        string sourceFolderSc = "D:\\Doris";
        string sortingFolderPr = "D:\\!Duplo";
        string sortingFolderSc = "D:\\!Doris";

        bool sortingViolations = true;
        int storageDays = 10;
        int storageSortingIntervalMinutes = 20;
        bool storageXML = true;
        bool storageСollage = false;
        bool storageVideo = false;

        static bool statusViewCamera = true;
        int statusViewCameraIntervalMinutes = 5;

        static bool statusServicesReplicator = true;
        int statusServicesReplicatorIntervalMinutes = 5;
        int restartingNoReplicationIntervalMinutes = 60;

        bool statusServicesExport = true;
        int statusServicesExportIntervalHours = 6;

        static string sqlSource = "(LOCAL)";
        static string sqlUser = "sa";
        static string sqlPassword = "1";

        static string connectionString = $@"Data Source={sqlSource};Initial Catalog=AVTO;User Id={sqlUser};Password={sqlPassword};Connection Timeout=60";

        int logFileName = 0;
        int statusExport = 0;
        int restartingReplication = 0;
        static bool statusWeb = true;
        static bool connectSQL = true;

        void LoadConfig()
        {
            LogWriteLine("------------------------- Monitoring Service Settings -------------------------");
            
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\VTMonitoring", true))
            {
                if (key.GetValue("FailureActions") == null)
                {
                    key.SetValue("FailureActions", new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x60, 0xea, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x60, 0xea, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x60, 0xea, 0x00, 0x00 });
                }
            }

            if (ConfigurationManager.AppSettings.Count != 0)
            {
                sourceFolderPr = ConfigurationManager.AppSettings["SourceFolderPr"];
                sortingFolderPr = ConfigurationManager.AppSettings["SortingFolderPr"];

                sourceFolderSc = ConfigurationManager.AppSettings["SourceFolderSc"];
                sortingFolderSc = ConfigurationManager.AppSettings["SortingFolderSc"];

                sortingViolations = Convert.ToBoolean(ConfigurationManager.AppSettings["SortingViolations"]);
                storageDays = Convert.ToInt32(ConfigurationManager.AppSettings["StorageDays"]);
                storageSortingIntervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["StorageSortingIntervalMinutes"]);
                storageXML = Convert.ToBoolean(ConfigurationManager.AppSettings["StorageXML"]);
                storageСollage = Convert.ToBoolean(ConfigurationManager.AppSettings["StorageСollage"]);
                storageVideo = Convert.ToBoolean(ConfigurationManager.AppSettings["StorageVideo"]);

                statusViewCamera = Convert.ToBoolean(ConfigurationManager.AppSettings["StatusViewCamera"]);
                statusViewCameraIntervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["StatusViewCameraIntervalMinutes"]);

                statusServicesReplicator = Convert.ToBoolean(ConfigurationManager.AppSettings["StatusServicesReplicator"]);
                statusServicesReplicatorIntervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["StatusServicesReplicatorIntervalMinutes"]);
                restartingNoReplicationIntervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["RestartingNoReplicationIntervalMinutes"]);

                statusServicesExport = Convert.ToBoolean(ConfigurationManager.AppSettings["StatusServicesExport"]);
                statusServicesExportIntervalHours = Convert.ToInt32(ConfigurationManager.AppSettings["StatusServicesExportIntervalHours"]);

                sqlSource = ConfigurationManager.AppSettings["SQLDataSource"];
                sqlUser = ConfigurationManager.AppSettings["SQLUser"];
                sqlPassword = ConfigurationManager.AppSettings["SQLPassword"];
            }

            if (statusServicesReplicator)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Vocord\VTTrafficReplicator"))
                {
                    if (key != null)
                    {
                        foreach (string ch in key.GetSubKeyNames())
                        {
                            using (RegistryKey key_ch = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Vocord\VTTrafficReplicator\" + ch))
                            {
                                if (key_ch != null)
                                {
                                    ReplicatorCh replicatorCh = new ReplicatorCh();

                                    replicatorCh.host = key_ch.GetValue("Host").ToString();
                                    replicatorCh.LastReplicationTime = key_ch.GetValue("LastReplicationTime").ToString();
                                    replicatorCh.LastReplicationTimeFt = Convert.ToInt64(key_ch.GetValue("LastReplicationTimeFt"));
                                    replicatorCh.OldLastReplicationTimeFt = replicatorCh.LastReplicationTimeFt;

                                    DateTime replicatorTime = DateTime.ParseExact(replicatorCh.LastReplicationTime, "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).Add(+localZone);
                                    string interval = DateTime.Now.Subtract(replicatorTime).TotalSeconds.ToString();

                                    replicatorCh.LastReplicationLocalTime = replicatorTime.ToString();
                                    replicatorCh.LagReplication = interval.Remove(interval.LastIndexOf(','));

                                    Replicator.Add(ch, replicatorCh);
                                }
                            }
                        }
                    }
                }
                if (Replicator.Count == 0)
                {
                    statusServicesReplicator = false;
                }
            }

            if (statusViewCamera)
            {
                string sqlViewCamera = $"SELECT Value FROM[AVTO].[dbo].[KeyValues] WHERE TypeKey = 'CrossRoadService.ViewCamera'";
                string viewCameraIP = @"\b((([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(\.)){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]))\b";
                Regex reg = new Regex(viewCameraIP);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        SqlCommand command = new SqlCommand(sqlViewCamera, connection);
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            var datajson = new JavaScriptSerializer().Deserialize<dynamic>(reader.GetValue(0).ToString());
                            foreach (Match ipCamera in reg.Matches(datajson["Connection"]))
                            {
                                ViewCamera.Add(ipCamera.ToString(), "-");
                                GetStatusViewCamera(ipCamera.ToString());
                                LogWriteLine($">>>>> Overview camera {ipCamera} added to status monitoring");
                            }
                        }
                        reader.Close();
                    }
                    catch (SqlException)
                    {
                        LogWriteLine($"********** No connection to SQL Server **********");
                        connectSQL = false;
                        connection.Close();
                    }
                    finally
                    {
                        if (connection.State == ConnectionState.Open)
                        {
                            connection.Close();
                        }
                    }
                }

                if (ViewCamera.Count == 0)
                {
                    statusViewCamera = false;
                }
            }

            if (sortingViolations)
            {
                var storageTimer = new System.Timers.Timer(storageSortingIntervalMinutes * 60000);
                storageTimer.Elapsed += OnStorageTimer;
                storageTimer.AutoReset = true;
                storageTimer.Enabled = true;
                LogWriteLine($">>>>> Violation sorting is enabled at {storageSortingIntervalMinutes} minute intervals");
            }

            if (statusViewCamera)
            {
                var viewCameraStatusTimer = new System.Timers.Timer(statusViewCameraIntervalMinutes * 60000);
                viewCameraStatusTimer.Elapsed += OnViewCameraStatusTimer;
                viewCameraStatusTimer.AutoReset = true;
                viewCameraStatusTimer.Enabled = true;
                LogWriteLine($">>>>> Monitoring of surveillance cameras is enabled at intervals of {statusViewCameraIntervalMinutes} minutes");
            }

            if (statusServicesReplicator)
            {
                var statusServicesReplicatorTimer = new System.Timers.Timer(statusServicesReplicatorIntervalMinutes * 60000);
                statusServicesReplicatorTimer.Elapsed += OnReplicatorStatusTimer;
                statusServicesReplicatorTimer.AutoReset = true;
                statusServicesReplicatorTimer.Enabled = true;
                LogWriteLine($">>>>> Replication service monitoring is enabled at {statusServicesReplicatorIntervalMinutes} minute intervals");
            }

            if (statusServicesReplicator)
            {
                var restartingServicesReplicatorTimer = new System.Timers.Timer(restartingNoReplicationIntervalMinutes * 60000);
                restartingServicesReplicatorTimer.Elapsed += OnRestartingServicesReplicatorTimer;
                restartingServicesReplicatorTimer.AutoReset = true;
                restartingServicesReplicatorTimer.Enabled = true;
                LogWriteLine($">>>>> Reboot if no replication is enabled interval {restartingNoReplicationIntervalMinutes} minutes");
            }

            if (statusServicesExport)
            {
                var statusServicesExportTimer = new System.Timers.Timer(statusServicesExportIntervalHours * 3600000);
                statusServicesExportTimer.Elapsed += OnExportStatusTimer;
                statusServicesExportTimer.AutoReset = true;
                statusServicesExportTimer.Enabled = true;
                LogWriteLine($">>>>> Export service monitoring is enabled at intervals of {statusServicesExportIntervalHours} hours.");
            }

            LogWriteLine("-------------------------------------------------------------------------------");
        }

        void OnStorageTimer(Object source, ElapsedEventArgs e)
        {
            SortingFiles(sourceFolderPr, sortingFolderPr);
            SortingFiles(sourceFolderSc, sortingFolderSc);
        }

        void GetStatusViewCamera(string ip)
        {
            PingReply pr = new Ping().Send(ip, 10000);
            if (pr.Status == IPStatus.Success)
            {
                ViewCamera[ip] = "200";
            }
            else
            {
                ViewCamera[ip] = "404";
            }
            //LogWriteLine($"DEBUG ********** View camera status {ip} = {ViewCamera[ip]} **********");
        }

        void OnViewCameraStatusTimer(Object source, ElapsedEventArgs e)
        {
            ICollection viewCameraKeys = ViewCamera.Keys;
            foreach (string ipViewCameraKey in viewCameraKeys)
            {
                GetStatusViewCamera(ipViewCameraKey);
            }
        }

        void OnReplicatorStatusTimer(Object source, ElapsedEventArgs e)
        {
            ICollection keys = Replicator.Keys;
            foreach (string ch in keys)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Vocord\VTTrafficReplicator\" + ch))
                {
                    if (key != null)
                    {
                        ReplicatorCh newStatus = (ReplicatorCh)Replicator[ch];
                        newStatus.OldLastReplicationTimeFt = newStatus.LastReplicationTimeFt;
                        newStatus.LastReplicationTime = key.GetValue("LastReplicationTime").ToString();
                        newStatus.LastReplicationTimeFt = Convert.ToInt64(key.GetValue("LastReplicationTimeFt"));

                        DateTime replicatorTime = DateTime.ParseExact(newStatus.LastReplicationTime, "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).Add(+localZone);
                        string interval = DateTime.Now.Subtract(replicatorTime).TotalSeconds.ToString();

                        newStatus.LastReplicationLocalTime = replicatorTime.ToString();
                        newStatus.LagReplication = interval.Remove(interval.LastIndexOf(','));
                    }
                }
            }
        }

        void OnRestartingServicesReplicatorTimer(Object source, ElapsedEventArgs e)
        {
            ICollection statusKey = Replicator.Keys;
            foreach (string key in statusKey)
            {
                ReplicatorCh repStatus = (ReplicatorCh)Replicator[key];

                if(repStatus.LastReplicationTimeFt == repStatus.OldLastReplicationTimeFt)
                {
                    restartingReplication++;
                    LogWriteLine($"***** No replication from crossroad {repStatus.host}, last replication time {repStatus.LastReplicationLocalTime} *****");
                }
                else
                {
                    restartingReplication = 0;
                }
            }

            if (restartingReplication > 0)
            {
                if (restartingReplication > Replicator.Count)
                {
                    LogWriteLine($"***** Reboot *****");
                    var cmd = new System.Diagnostics.ProcessStartInfo("shutdown.exe", "-r -t 0");
                    cmd.CreateNoWindow = true;
                    cmd.UseShellExecute = false;
                    cmd.ErrorDialog = false;
                    Process.Start(cmd);
                }
                else
                {
                    StopService("VTTrafficReplicator");
                    StopService("VTViolations");
                    StartService("VTTrafficReplicator");
                    StartService("VTViolations");
                }
            }
        }

        void OnExportStatusTimer(Object source, ElapsedEventArgs e)
        {
            string[] files;
            if (Directory.Exists(sourceFolderPr))
            {
                files = Directory.GetFiles(sourceFolderPr, "*.xml", SearchOption.AllDirectories);
                statusExport += files.Length;
            }
            if (Directory.Exists(sourceFolderSc))
            {
                files = Directory.GetFiles(sourceFolderSc, "*.xml", SearchOption.AllDirectories);
                statusExport += files.Length;
            }
            if (statusExport == 0)
            {
                LogWriteLine($"***** Export service, there were no unloading violations for {statusServicesExportIntervalHours} hours. *****");
                StopService("VTTrafficExport");
                StopService("VTViolations");
                StartService("VTTrafficExport");
                StartService("VTViolations");
            }
            statusExport = 0;
        }

        void LogWriteLine(string message)
        {
            if (!(Directory.Exists(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log")))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log");
            }

            string logDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log";

            string[] tempfiles = Directory.GetFiles(logDir, "-log.txt", SearchOption.AllDirectories);

            if(tempfiles.Count() != 0)
            {
                foreach (string file in tempfiles)
                {
                    string names = Path.GetFileName(file);
                    Regex regex = new Regex(@"\d{4}-");
                    if (regex.IsMatch(names))
                    {
                        int number = (int.Parse(names.Remove(names.IndexOf("-"))));
                        if (number > logFileName)
                        {
                            logFileName = number;
                        }
                    }
                }
            }

            string name = logFileName.ToString("0000");
            FileInfo fileInfo = new FileInfo(logDir + $"\\{name}-log.txt");
            using (StreamWriter sw = fileInfo.AppendText())
            {
                sw.WriteLine(String.Format("{0:yyMMdd hh:mm:ss} {1}", DateTime.Now.ToString(), message));
                sw.Close();
                if (fileInfo.Length > 20480)
                {
                    logFileName++;
                }

                string[] delTimefiles = Directory.GetFiles(logDir, "*", SearchOption.AllDirectories);
                foreach (string delTimefile in delTimefiles)
                {
                    FileInfo fi = new FileInfo(delTimefile);
                    if (fi.CreationTime < DateTime.Now.AddDays(-storageDays)) { fi.Delete(); }
                }
            }
        }

        void StartService(string serviceName)
        {
            ServiceController service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(2));
            }
            LogWriteLine($">>>> Service {serviceName} status >>>> {service.Status} <<<<");
        }

        void StopService(string serviceName)
        {
            ServiceController service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(2));
                if (service.Status != ServiceControllerStatus.StopPending)
                {
                    foreach (var process in Process.GetProcessesByName(serviceName))
                    {
                        process.Kill();
                        LogWriteLine($"********** Service {serviceName} Kill **********");
                    }
                }
            }
            LogWriteLine($">>>> Service {serviceName} status >>>> {service.Status} <<<<");
        }

        static void ThreadWEBServer() 
        {
            serverWeb = new HttpListener();
            serverWeb.Prefixes.Add(@"http://+:8090/");
            serverWeb.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            serverWeb.Start();
            while (statusWeb)
            {
                ProcessRequest();
            }
        }

        static void ProcessRequest()
        {
            var result = serverWeb.BeginGetContext(ListenerCallback, serverWeb);
            var startNew = Stopwatch.StartNew();
            result.AsyncWaitHandle.WaitOne();
            startNew.Stop();
        }

        static void ListenerCallback(IAsyncResult result)
        {
            var HttpResponse = serverWeb.EndGetContext(result);
            int deltamin = Convert.ToInt32(HttpResponse.Request.QueryString["minutes"]);

            DateTime endDateTime = DateTime.UtcNow;
            DateTime sqlDateTime = endDateTime.AddMinutes(-deltamin);
            int violations = 0;
            string sqlAlarm = $"SELECT COUNT_BIG(CARS_ID) FROM[AVTO].[dbo].[CARS_VIOLATIONS] WHERE CHECKTIME > '{sqlDateTime:s}'";

            if (connectSQL)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        SqlCommand command = new SqlCommand(sqlAlarm, connection);
                        SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            violations = Convert.ToInt32(reader.GetValue(0));
                        }
                        reader.Close();
                    }
                    catch (SqlException)
                    {
                        connection.Close();
                    }
                    finally
                    {
                        if (connection.State == ConnectionState.Open)
                        {
                            connection.Close();
                        }
                    }
                }
            }

            string json = "{\"getDateTime\":\"" + DateTime.Now.ToString() + "\"";

            if (connectSQL)
            {
                json += ",\"violations\": " + violations;
            }

            if (statusServicesReplicator)
            {
                json += ",\"replicator\":[";
                int r = 0;
                foreach (DictionaryEntry replicatorKey in Replicator)
                {
                    ReplicatorCh repStatus = (ReplicatorCh)replicatorKey.Value;
                    r++;
                    json += "{\"host\":\"" + repStatus.host + "\",\"lastReplicator\":\"" + repStatus.LastReplicationLocalTime + "\",\"lastReplicatorSec\":" + repStatus.LagReplication + "}";
                    if (r < Replicator.Count)
                    {
                        json += ",";
                    }
                }
                json += "]";
            }

            if (statusViewCamera)
            {
                json += ",\"viewCamera\":[";
                int c = 0;
                foreach (DictionaryEntry ViewCameraKey in ViewCamera)
                {
                    c++;
                    json += "{\"ip\":\"" + ViewCameraKey.Key + "\",\"status\":" + ViewCameraKey.Value + "}";
                    if (c < ViewCamera.Count)
                    {
                        json += ",";
                    }
                }
                json += "]";
            }

            json += "}";

            HttpResponse.Response.Headers.Add("Content-Type", "application/json");
            HttpResponse.Response.StatusCode = 200;
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            HttpResponse.Response.ContentLength64 = buffer.Length;
            HttpResponse.Response.OutputStream.Write(buffer, 0, buffer.Length);
            HttpResponse.Response.Close();
        }

        void processDirectory(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                processDirectory(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }

        void SortingFiles(string sourcePath, string outPath)
        {
            if (Directory.Exists(sourcePath))
            {
                XmlDocument xFile = new XmlDocument();
                string[] files = Directory.GetFiles(sourcePath, "*.xml", SearchOption.AllDirectories);
                int countFiles = files.Length;
                statusExport += countFiles;
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file);
                    string PathSour = file.Remove(file.LastIndexOf("\\"));
                    string nameRemote = name.Remove(name.LastIndexOf("_"));
                    xFile.Load(file);
                    if (xFile.SelectSingleNode("//v_photo_ts") != null)
                    {
                        XmlNodeList violation_time = xFile.GetElementsByTagName("v_time_check");
                        string data = violation_time[0].InnerText.Remove(violation_time[0].InnerText.IndexOf("T"));
                        XmlNodeList violation_camera = xFile.GetElementsByTagName("v_camera");
                        XmlNodeList violation_pr_viol = xFile.GetElementsByTagName("v_pr_viol");

                        string Path = outPath + "\\" + data + "\\" + (string)ViolationCode[violation_pr_viol[0].InnerText] + "\\" + violation_camera[0].InnerText + "\\";

                        Console.WriteLine(PathSour);

                        if (!(Directory.Exists(Path)))
                        {
                            Directory.CreateDirectory(Path);
                        }

                        if (storageXML)
                        {
                            File.Copy(file, (Path + name), true);
                        }

                        if (storageСollage && File.Exists(PathSour + "\\" + nameRemote + "_car.jpg"))
                        {
                            File.Copy((PathSour + "\\" + nameRemote + "_car.jpg"), (Path + nameRemote + "_car.jpg"), true);
                        }

                        if (storageVideo && File.Exists(PathSour + "\\" + nameRemote + "__1video.mp4"))
                        {
                            File.Copy((PathSour + "\\" + nameRemote + "__1video.mp4"), (Path + nameRemote + "__1video.mp4"), true);
                        }

                        if (storageVideo && File.Exists(PathSour + "\\" + nameRemote + "__2video.mp4"))
                        {
                            File.Copy((PathSour + "\\" + nameRemote + "__2video.mp4"), (Path + nameRemote + "__2video.mp4"), true);
                        }

                        string[] delFiles = Directory.GetFiles(sourcePath, (nameRemote + "*"), SearchOption.AllDirectories);
                        foreach (string delFile in delFiles)
                        {
                            File.Delete(delFile);
                        }

                        processDirectory(sourcePath);

                        string[] delTimefiles = Directory.GetFiles(outPath, "*", SearchOption.AllDirectories);
                        foreach (string delTimefile in delTimefiles)
                        {
                            FileInfo fi = new FileInfo(delTimefile);
                            if (fi.CreationTime < DateTime.Now.AddDays(-storageDays)) { fi.Delete(); }
                        }
                        processDirectory(outPath);
                    }
                }
                LogWriteLine($">>>>>>>> Sorted {countFiles} violations");
            }
        }

        protected override void OnStart(string[] args)
        {
            LogWriteLine("*******************************************************************************");
            LogWriteLine("************************** Service Monitoring START ***************************");
            LogWriteLine("*******************************************************************************");
            LoadConfig();
            HashVuolation();
            WEBServer.Start();
        }

        protected override void OnStop()
        {
            statusWeb = false;
            WEBServer.Interrupt();
            LogWriteLine("*******************************************************************************");
            LogWriteLine("*************************** Service Monitoring STOP ***************************");
            LogWriteLine("*******************************************************************************");
        }
    }
}
