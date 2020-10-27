using CsvHelper;
using DevExpress.Charts.Model;
using DevExpress.Xpf.Core.Native;
using DevExpress.XtraReports.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data.Linq;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;

namespace ApexCOINESB
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : DevExpress.Xpf.Core.ThemedWindow, INotifyPropertyChanged
    {
        private static List<StatusLine> _StatusLines = new List<StatusLine>();  //Status report lines
        private bool _POSent = false;
        private SqlConnectionStringBuilder _SqlConnBuilder;
        private List<POAmendmentCSVRecord> _AmendmentList = new List<POAmendmentCSVRecord>();
        private string _POAmendDirectory = String.Empty;

        public IEnumerable<StatusLine> GetStatusLines() => _StatusLines;

        private ObservableCollection<string> _ProgressInfo;
        public ObservableCollection<string> ProgressInfo
        {
            get { return _ProgressInfo; }
            set
            {
                if (value != _ProgressInfo)
                {
                    this._ProgressInfo = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = this;
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ApexCOINESB.Properties.Settings.ApexVECConnectionString"];
            if (settings != null)
            {
                string connection = settings.ConnectionString;
                _SqlConnBuilder = new SqlConnectionStringBuilder(connection);

                //See if we have all of the requisite tables built and Apex is at the minimum version
                using (System.Data.SqlClient.SqlConnection conn = new SqlConnection(_SqlConnBuilder.ConnectionString))
                {
                    conn.Open();
                    System.Data.SqlClient.SqlCommand cmd;
                    cmd = new System.Data.SqlClient.SqlCommand("SELECT Version FROM System", conn);
                    int version = (int?)cmd.ExecuteScalar() ?? 0;
                    if (version < 36)
                    {
                        MessageBox.Show("This program requires Apex with database version 36 or greater.");
                        Application.Current.Shutdown();
                    }

                    conn.Close();
                }
            }
            else
            {
                MessageBox.Show("Unable to set connection string, program terminating.");
                Application.Current.Shutdown();
                return;
            }

            ProgressInfo = new ObservableCollection<string>();
            ProgressInfo.Add("Press the Send button to send Apex P/Os to COINS");

            _POAmendDirectory = Properties.Settings.Default.POAmendDirectory;

            if (!CheckDirectoryAccess(_POAmendDirectory))
            {
                MessageBox.Show("Unable to write to the P/O Amend directory, process aborting.");
                Application.Current.Shutdown();
            }
        }

        private static bool CheckDirectoryAccess(string directory)
        {
            bool success = false;
            string fullPath = directory + @"\Test.txt";

            if (Directory.Exists(directory))
            {
                try
                {
                    using (FileStream fs = new FileStream(fullPath, FileMode.CreateNew,
                                                                    FileAccess.Write))
                    {
                        fs.WriteByte(0xff);
                    }

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        success = true;
                    }
                }
                catch (Exception)
                {
                    success = false;
                }
            }
            else success = false;

            return success;
        }

        private async void BtnSendPOs_Click(object sender, RoutedEventArgs e)
        {
            ProgressInfo = new ObservableCollection<string>();
            ProgressInfo.Add("Sending Apex P/Os...");

            _POSent = false;

            Cursor = Cursors.Wait;
            try
            {
                await SendApexPOAmendments();
                await SendApexPOs();

                ProgressInfo.Add("Process complete.");
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }

            if (_POSent)
            {
                XtraReport report = new StatusReport();
                var statusReportViewer = new ReportViewer();
                statusReportViewer.PreviewControl.DocumentSource = report;
                report.CreateDocument();
                statusReportViewer.ShowDialog();
            }
        }

        #region SendPOs
        private async Task SendApexPOs()
        {
            using (ApexDataDataContext apexData = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
            {
                try
                {
                    _StatusLines.Clear();

                    var dlo = new DataLoadOptions();
                    dlo.LoadWith<PO>(p => p.POLines);  //grab the lines at the same time for efficiency
                    apexData.LoadOptions = dlo;

                    List<PO> apexPOList = apexData.POs.Where(p => (p.Vendor ?? "") != ""
                                            && (((p.Job ?? "") != "") || ((p.WorkOrd ?? "") != ""))
                                            && ((p.ExpBatch == 0) || (p.ExpBatch == -1))
                                            && ((p.POStatus == "F" || p.POStatus == "C"))).ToList();

                    if (apexPOList.Count == 0)
                    {
                        ProgressInfo.Add("There are no purchase orders to send.");
                    }
                    else
                    {
                        _POSent = true;  //We have a valid P/O to send so present the interface status report when complete

                        foreach (PO porec in apexPOList)
                        {
                            await ProcessPO(porec, apexData);
                        }
                    }
                }

                catch (Exception ex)
                {
                    MessageBox.Show("Error:" + ex.Message, "Unexpected error");
                }
            }
        }

        private async Task ProcessPO(PO porec, ApexDataDataContext apexData)
        {
            ProgressInfo.Add($"Sending P/O {porec.Po1.Trim()}");

            string reportMessage;

            ApexSystem apexSystem = apexData.ApexSystems.FirstOrDefault();

            COINSESBService.COINSInterfaceHeader header;
            COINSESBService.COINSInterfacePo_hdrRow poheader;

            header = new COINSESBService.COINSInterfaceHeader
            {
                id = (apexSystem.ExportBatch ?? 1).ToString(),
                confirm = COINSESBService.COINSInterfaceHeaderConfirm.no,
                UserID = "pkramer",
                From = "Apex",
                HostName = "coinsoa",
                Environment = "live",
                Created = DateTime.UtcNow,
                Login = new COINSESBService.COINSInterfaceHeaderLogin()
                {
                    User = "pkramer",
                    Password = "<password>",
                    CID = 1
                }
            };

            if (!String.IsNullOrEmpty(porec.Job)) //Job based P/O
            {
                poheader = BuildJobBasedPOHeader(porec, apexSystem);

                poheader.po_lineRow = new COINSESBService.COINSInterfacePo_hdrRowPo_lineRow[porec.POLines.Count];

                for (int i = 0; i < porec.POLines.Count; i++)
                {
                    POLine polinerec = porec.POLines[i];

                    string jobphase = String.IsNullOrEmpty(porec.JobPhase) ? "00" : porec.JobPhase;
                    string wbs;

                    COINSESB_WB wbsRec = apexData.COINSESB_WBs.Where(s => s.Job == porec.Job).FirstOrDefault();
                    if (wbsRec == null || !(wbsRec.UsesActivity ?? false))
                        wbs = jobphase + "-";
                    else
                        wbs = jobphase + "-00-";

                    COINSESB_ExpL expL = apexData.COINSESB_ExpLs.Where(s => s.PO == polinerec.Po && s.POLine == polinerec.PoLine1).SingleOrDefault();
                    int? pol_seq = expL?.POL_Seq;
                    decimal? lastPrice = expL?.LastPrice;

                    string cat = "MA";
                    string schedule = "STD";
                    Job job = apexData.Jobs.Where(s => s.Job1 == porec.Job).FirstOrDefault();
                    if (job != null)
                    {
                        schedule = job.Schedule;
                        Costcode ccd = apexData.Costcodes.Where(s => s.Schedule == schedule && s.CostCode1 == polinerec.CostCode).FirstOrDefault();
                        if (ccd != null) cat = ccd.GL;
                    }

                    COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
                        polinerow = BuildJobBasedPOLine(polinerec, wbs, pol_seq, cat,
                        expL != null && ((polinerec.Price ?? 0) != (lastPrice ?? 0)), porec.ShipDate ?? DateTime.Now);
                    poheader.po_lineRow[i] = polinerow;
                }
            }
            else //Work Order based P/O
            {
                poheader = BuildWOBasedPOHeader(porec, apexSystem);

                poheader.po_lineRow = new COINSESBService.COINSInterfacePo_hdrRowPo_lineRow[porec.POLines.Count];

                for (int i = 0; i < porec.POLines.Count; i++)
                {
                    POLine polinerec = porec.POLines[i];

                    COINSESB_ExpL expL = apexData.COINSESB_ExpLs.Where(s => s.PO == polinerec.Po && s.POLine == polinerec.PoLine1).SingleOrDefault();
                    int? pol_seq = expL?.POL_Seq;
                    decimal? lastPrice = expL?.LastPrice;

                    string cat = "MA";
                    string schedule = "STD";
                    Costcode ccd = apexData.Costcodes.Where(s => s.Schedule == schedule && s.CostCode1 == polinerec.CostCode).FirstOrDefault();
                    if (ccd != null) cat = ccd.GL;

                    COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
                        polinerow = BuildWOBasedPOLine(porec, polinerec, pol_seq, cat,
                        expL != null && ((polinerec.Price ?? 0) != (lastPrice ?? 0)), porec.ShipDate ?? DateTime.Now);
                    poheader.po_lineRow[i] = polinerow;
                }
            }

            COINSESBService.COINSInterfacePo_hdrRow[] poheaderrows = new COINSESBService.COINSInterfacePo_hdrRow[1];
            poheaderrows[0] = poheader;

            var client = new COINSESBService.COINSInterfacePortClient("COINSInterface");
            var actionrequest = new COINSESBService.doActionRequest
            {
                Header = header,
                Body = poheaderrows
            };

#if SENDXMLDIAGNOSTIC
            XmlSerializer serializer = new XmlSerializer(typeof(COINSESBService.doActionRequest));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(stream, actionrequest);
                stream.Seek(0, SeekOrigin.Begin);
                string capturedXML = stream.ReadToString();
                HttpClient postClient = new HttpClient();
                var postResponse = await postClient.PostAsync("http://apexcoin.apexpurchasing.com/api/coinsxml",
                    new StringContent(capturedXML));
            }
#endif

            var response = await client.doActionAsync(actionrequest);

            if (response.Header.action == COINSESBService.COINSInterfaceResponseHeaderAction.RESPONSE)
            {
                if (porec.ExpSent == "T")
                    reportMessage = "Sent to COINS (Amendment)";
                else
                    reportMessage = "Sent to COINS";

                porec.ExpSent = "T";
                var lines = response.Body.po_hdrRow[0].po_lineRow;
                int lineRowNumber = 0;
                foreach (var POLine in porec.POLines)
                {
                    var pol_seq = lines[lineRowNumber++].pol_seq;
                    bool newRecord = false;
                    var expL = apexData.COINSESB_ExpLs.Where(s => s.PO == POLine.Po && s.POLine == POLine.PoLine1).SingleOrDefault();
                    if (expL == null)
                    {
                        expL = new COINSESB_ExpL
                        {
                            PO = POLine.Po,
                            POLine = POLine.PoLine1
                        };
                        newRecord = true;
                    }

                    expL.POL_Seq = pol_seq;
                    expL.LastAmt = POLine.Ext;
                    expL.LastPrice = POLine.Price;
                    if (newRecord) apexData.COINSESB_ExpLs.InsertOnSubmit(expL);
                    apexData.SubmitChanges();
                }
            }
            else if (response.Header.action == COINSESBService.COINSInterfaceResponseHeaderAction.EXCEPTION)
            {
                reportMessage = response.Body.Exception.Exception;
            }
            else
            {
                reportMessage = "Unknown response from COINS";
            }

            porec.ExpBatch = apexSystem.ExportBatch ?? 1;
            apexSystem.ExportBatch += 1;
            apexData.SubmitChanges();

            _StatusLines.Add(new StatusLine
            {
                PO = porec.Po1?.Trim(),
                Job = porec.Job?.Trim(),
                WorkOrd = porec.WorkOrd?.Trim(),
                Vendor = porec.Vendor?.Trim(),
                Message = reportMessage
            });
        }

        private static COINSESBService.COINSInterfacePo_hdrRow BuildJobBasedPOHeader(PO porec, ApexSystem apexSystem) =>
            new COINSESBService.COINSInterfacePo_hdrRow
            {
                id = (apexSystem.ExportBatch ?? 1).ToString(),
                poh_ordno = porec.Po1.Trim(),
                rsp_action = "I",
                commitOrder = true,
                poh_mpo = "M",
                pot_type = "N",
                poh_headoffice = true,
                poh_confirm = false,
                job_jobph = porec.Job.Trim(),
                poh_accno = porec.Vendor.Trim(),
                poh_name = porec.VendorName,
                poh_odate = porec.EntDate ?? DateTime.Now,
                poh_ddate = porec.ShipDate ?? DateTime.Now,
                poh_ddateSpecified = true,
                poh_desc = porec.PODesc,
                pob_code = "pkramer",
                poh_reqdby = porec.ContactID
            };

        private static COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
            BuildJobBasedPOLine(POLine polinerec, string wbs, int? pol_seq, string cat, bool pricechange, DateTime duedate) =>
            new COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
            {
                pol_type = "C",
                pol_code = "CM-MA",
                pol_seq = pol_seq ?? 0,
                pol_seqSpecified = pol_seq != null,
                rsp_action = pol_seq != null ? "U" : "I",
                pol_desc = String.IsNullOrEmpty(polinerec.MfgDesc) ? "***Unknown***" : polinerec.MfgDesc,
                jwb_code = wbs,
                pol_qty = ((decimal)(polinerec.QtyOrd ?? 0) - (decimal)(polinerec.QtyIvc ?? 0)),
                pol_qtySpecified = true,
                pol_uoq = "E",
                pol_price = polinerec.Price ?? 0,
                pol_priceSpecified = true,
                pol_effdate = duedate,
                pol_effdateSpecified = pricechange,
                pol_per = polinerec.UM,
                pol_ddate = duedate,
                pol_ddateSpecified = true,
                pol_CostHead = polinerec.CostCode?.Trim(),
                pol_cat = "MA" //cat later, but amendments won't work
            };

        private static string DetermineServiceJob(PO porec)
        {
            string selectedJobPh;
            string poPrefix;

            poPrefix = porec.Po1.Trim().Substring(0, 1);
            switch (poPrefix)
            {
                case "1":
                    selectedJobPh = "S046";
                    break;

                case "2":
                    selectedJobPh = "S076";
                    break;

                case "3":
                    selectedJobPh = "S191";
                    break;

                case "4":
                    selectedJobPh = "S192";
                    break;

                default:
                    selectedJobPh = "***Unknown***";
                    break;
            }

            return selectedJobPh;
        }

        private static COINSESBService.COINSInterfacePo_hdrRow BuildWOBasedPOHeader(PO porec, ApexSystem apexSystem)
        {
            string selectedJobPh = DetermineServiceJob(porec);

            return new COINSESBService.COINSInterfacePo_hdrRow
            {
                id = (apexSystem.ExportBatch ?? 1).ToString(),
                poh_ordno = porec.Po1.Trim(),
                rsp_action = "I",
                commitOrder = true,
                poh_mpo = "M",
                pot_type = "N",
                poh_headoffice = true,
                poh_confirm = false,
                job_jobph = selectedJobPh,
                poh_accno = porec.Vendor.Trim(),
                poh_name = porec.VendorName,
                poh_odate = porec.EntDate ?? DateTime.Now,
                poh_ddate = porec.ShipDate ?? DateTime.Now,
                poh_ddateSpecified = true,
                poh_desc = porec.PODesc,
                pob_code = "pkramer",
                poh_reqdby = porec.ContactID
            };
        }

        private static COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
            BuildWOBasedPOLine(PO porec, POLine polinerec, int? pol_seq, string cat, bool pricechange, DateTime duedate) =>
            new COINSESBService.COINSInterfacePo_hdrRowPo_lineRow
            {
                pol_type = "C",
                pol_code = "CM-MA",
                pol_seq = pol_seq ?? 0,
                pol_seqSpecified = pol_seq != null,
                rsp_action = pol_seq != null ? "U" : "I",
                pol_desc = String.IsNullOrEmpty(polinerec.MfgDesc) ? "***Unknown***" : polinerec.MfgDesc,
                jwb_code = porec.WorkOrd.Trim(),
                pol_qty = ((decimal)(polinerec.QtyOrd ?? 0) - (decimal)(polinerec.QtyIvc ?? 0)),
                pol_qtySpecified = true,
                pol_uoq = "E",
                pol_price = polinerec.Price ?? 0,
                pol_priceSpecified = true,
                pol_effdate = duedate,
                pol_effdateSpecified = pricechange,
                pol_per = polinerec.UM,
                pol_ddate = duedate,
                pol_ddateSpecified = true,
                pol_CostHead = "NAB",
                pol_cat = "MA" //cat later, but amendments won't work
            };

#endregion

        private async Task SendApexPOAmendments()
        {
            using (ApexDataDataContext apexData = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
            {
                try
                {
                    var dlo = new DataLoadOptions();
                    dlo.LoadWith<PO>(p => p.POLines);  //grab the lines at the same time for efficiency
                    apexData.LoadOptions = dlo;

                    List<PO> apexPOList = apexData.POs.Where(p => (p.Vendor ?? "") != ""
                        && (((p.Job ?? "") != "") || ((p.WorkOrd ?? "") != ""))
                        && ((p.ExpBatch == 0) || (p.ExpBatch == -1)) && (p.ExpSent ?? "F") == "T"
                        && (p.POStatus == "F" || p.POStatus == "C")).ToList();

                    if (apexPOList.Count == 0)
                    {
                        ProgressInfo.Add("There are no purchase order amendments to send.");
                    }
                    else
                    {
                        _POSent = true;  //We have a valid P/O to send so present the interface status report when complete

                        using (var writer = new StreamWriter(_POAmendDirectory + @"\POAmend-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv"))
                        using (var csv = new CsvWriter(writer))
                        {
                            _AmendmentList.Clear();
                            //_AmendmentList.Add(POAmendmentCSVRecord.BuildLine1());
                            _AmendmentList.Add(POAmendmentCSVRecord.BuildLine2());

                            foreach (PO porec in apexPOList)
                            {
                                _AmendmentList.AddRange(await ProcessPOAmendment(porec, apexData));
                            }

                            csv.WriteRecords(_AmendmentList);
                        }
                    }
                }

                catch (Exception ex)
                {
                    MessageBox.Show("Error:" + ex.Message, "Unexpected error");
                }
            }
        }

        private async Task<List<POAmendmentCSVRecord>> ProcessPOAmendment(PO porec, ApexDataDataContext apexData)
        {
            ProgressInfo.Add($"Filing P/O amendment {porec.Po1.Trim()}");

            ApexSystem apexSystem = apexData.ApexSystems.FirstOrDefault();

            List<POAmendmentCSVRecord> csvList = new List<POAmendmentCSVRecord>();

            if (porec.Job != null && porec.Job != "") //Job based P/O
            {
                POAmendmentCSVRecord pocsvrec = BuildJobBasedAmendmentHeader(porec, apexSystem);

                for (int i = 0; i < porec.POLines.Count; i++)
                {
                    POLine polinerec = porec.POLines[i];

                    string jobphase = String.IsNullOrEmpty(porec.JobPhase) ? "00" : porec.JobPhase;
                    string wbs;

                    COINSESB_WB wbsRec = apexData.COINSESB_WBs.Where(s => s.Job == porec.Job).FirstOrDefault();
                    if (wbsRec == null || !(wbsRec.UsesActivity ?? false))
                        wbs = jobphase + "-";
                    else
                        wbs = jobphase + "-00-";

                    pocsvrec = BuildJobBasedAmendmentLine(polinerec, wbs, pocsvrec);
                    csvList.Add(pocsvrec);
                    pocsvrec = CreateNotNewCSVRec(); //Clear the header portion for subsequent line items
                }
            }
            else //Work Order based P/O
            {
                POAmendmentCSVRecord pocsvrec = BuildWOBasedAmendmentHeader(porec, apexSystem);

                for (int i = 0; i < porec.POLines.Count; i++)
                {
                    POLine polinerec = porec.POLines[i];

                    pocsvrec = BuildWOBasedAmemdmentPOLine(porec, polinerec, pocsvrec);
                    csvList.Add(pocsvrec);
                    pocsvrec = CreateNotNewCSVRec(); //Clear the header portion for subsequent line items
                }
            }

            _StatusLines.Add(new StatusLine
            {
                PO = porec.Po1?.Trim(),
                Job = porec.Job?.Trim(),
                WorkOrd = porec.WorkOrd?.Trim(),
                Vendor = porec.Vendor?.Trim(),
                Message = "Amendment written to file"
            });

            return csvList;
        }

        private static POAmendmentCSVRecord CreateNotNewCSVRec()
        {
            POAmendmentCSVRecord notnewcsvrec = new POAmendmentCSVRecord();
            notnewcsvrec.NewPO = "N";
            return notnewcsvrec;
        }

        private POAmendmentCSVRecord BuildJobBasedAmendmentHeader(PO porec, ApexSystem apexSystem) =>
            new POAmendmentCSVRecord
            {
                NewPO = "Y",
                PO = porec.Po1.Trim(),
                CONum = (apexSystem.ExportBatch ?? 1).ToString(),
                VariationOrderType = "A",
                POMajorType = "M",
                OrderType = "N",
                HeadOrder = "Y", //this is the value from the web service, contradicting Will's example
                ConfirmationOrder = "N",
                Job = porec.Job.Trim(),
                Account = porec.Vendor.Trim(),
                Currency = "USD",
                Attention = porec.VendorAttn,
                OrderDate = (porec.EntDate ?? DateTime.Now).ToString(),
                DueDate = (porec.ShipDate ?? DateTime.Now).ToString(),
                Description = porec.PODesc,
                Buyer = "pkramer"
            };

        private POAmendmentCSVRecord BuildJobBasedAmendmentLine(POLine polinerec, string wbs, POAmendmentCSVRecord pocsvrec)
        {
            pocsvrec.OrderLineType = "m";  //Changed from "C", which worked in testing but not production
            pocsvrec.Code = "CM-MA";
            pocsvrec.ClauseCode = String.Empty;
            pocsvrec.OrderLineDescription = String.IsNullOrEmpty(polinerec.MfgDesc) ? "***Unknown***" : polinerec.MfgDesc;
            pocsvrec.WBSCode = wbs;
            pocsvrec.CostCode = polinerec.CostCode?.Trim();
            pocsvrec.CostCategory = "MA";
            pocsvrec.Quantity = ((decimal)(polinerec.QtyOrd ?? 0) - (decimal)(polinerec.QtyIvc ?? 0)).ToString();
            pocsvrec.Unit = "E";
            pocsvrec.Price = (polinerec.Price ?? 0).ToString();
            pocsvrec.Per = polinerec.UM;
            return pocsvrec;
        }

        private POAmendmentCSVRecord BuildWOBasedAmendmentHeader(PO porec, ApexSystem apexSystem)
        {
            string selectedJobPh = DetermineServiceJob(porec);

            return new POAmendmentCSVRecord
            {
                NewPO = "Y",
                PO = porec.Po1.Trim(),
                CONum = (apexSystem.ExportBatch ?? 1).ToString(),
                VariationOrderType = "A",
                POMajorType = "M",
                OrderType = "N",
                HeadOrder = "Y", //this is the value from the web service, contradicting Will's example
                ConfirmationOrder = "N",
                Job = selectedJobPh,
                Account = porec.Vendor.Trim(),
                Currency = "USD",
                Attention = porec.VendorAttn,
                OrderDate = (porec.EntDate ?? DateTime.Now).ToString(),
                DueDate = (porec.ShipDate ?? DateTime.Now).ToString(),
                Description = porec.PODesc,
                Buyer = "pkramer"
            };
        }

        private POAmendmentCSVRecord BuildWOBasedAmemdmentPOLine(PO porec, POLine polinerec, POAmendmentCSVRecord pocsvrec)
        {
            pocsvrec.OrderLineType = "m";
            pocsvrec.Code = "CM-MA";
            pocsvrec.ClauseCode = String.Empty;
            pocsvrec.OrderLineDescription = String.IsNullOrEmpty(polinerec.MfgDesc) ? "***Unknown***" : polinerec.MfgDesc;
            pocsvrec.WBSCode = porec.WorkOrd.Trim();
            pocsvrec.CostCode = "NAB";
            pocsvrec.CostCategory = "MA";
            pocsvrec.Quantity = ((decimal)(polinerec.QtyOrd ?? 0) - (decimal)(polinerec.QtyIvc ?? 0)).ToString();
            pocsvrec.Unit = "E";
            pocsvrec.Price = (polinerec.Price ?? 0).ToString();
            pocsvrec.Per = polinerec.UM;
            return pocsvrec;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnResetPO_Click(object sender, RoutedEventArgs e)
        {
            //This is ugly for now but I am in a hurry to get this functionality
            var po = Microsoft.VisualBasic.Interaction.InputBox("P/O:", "Reset a P/O to show as not sent to COINS");
            using (ApexDataDataContext apexData = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
            {
                PO apexPO = apexData.POs.Where(s => s.Po1 == po.PadLeft(12)).SingleOrDefault();
                if (apexPO == null)
                {
                    MessageBox.Show("P/O not found", "Unable to Reset P/O", MessageBoxButton.OK);
                    return;
                }
                else
                {
                    apexPO.ExpBatch = -1;
                    apexPO.ExpSent = "F";
                    apexData.SubmitChanges();

                    apexData.ExecuteCommand("DELETE FROM COINSESB_ExpL WHERE PO = '" + apexPO.Po1 + "'");
                    MessageBox.Show("P/O reset as requested", "Reset Successful", MessageBoxButton.OK);
                }
            }
        }

        private void btnCOINSData_Click(object sender, RoutedEventArgs e)
        {
            COINSData coinsdatawindow = new COINSData();
            coinsdatawindow.Show();
        }

        private void btnCOINSWBS_Click(object sender, RoutedEventArgs e)
        {
            COINSWBS coinswbswindow = new COINSWBS();
            coinswbswindow.Show();
        }
    }
}
