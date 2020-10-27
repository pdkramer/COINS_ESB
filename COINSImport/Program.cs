using COINS_ESB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace COINSImport
{
    class Program
    {
        const string _APEXCOINURL = "http://apexcoin.apexpurchasing.com/";

        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("COINSImport:  Transfer information from COINS into Apex (v2: 04/08/2020)");

            if (args.Length != 1 || args[0].ToLower() != "/go")
            {
                ShowHelpPage();
#if DEBUG
                Console.ReadLine();
#endif
                return;
            }

            try
            {
                GetCOINSDataAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
#if DEBUG
                Console.ReadLine();
#endif
            }
        }

        private static void ShowHelpPage()
        {
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("COINSImport /go");
            Console.WriteLine();
            Console.WriteLine("   /go          Transfer COINS data to Apex");
            Console.WriteLine();
            Console.WriteLine("If /go is not specified, this help page is returned.");
            Console.WriteLine();
        }

        protected static async Task<IEnumerable<CoinsXml>> GetCOINSJsonAsync(string path)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(path);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                IEnumerable<CoinsXml> coinsxmllist = null;
                HttpResponseMessage response = await client.GetAsync("api/coinsxml");
                if (response.IsSuccessStatusCode)
                {
                    coinsxmllist = await response.Content.ReadAsAsync<IEnumerable<CoinsXml>>();
                }
                return coinsxmllist;
            }
        }

        protected static async Task DeleteCOINSIDAsync(string path, long ID)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(path);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                await client.DeleteAsync("api/coinsxml/" + ID.ToString());
            }
        }

        private static async Task GetCOINSDataAsync()
        {
            bool moreData = true;
            IEnumerable<CoinsXml> coinsxmllist;

            while (moreData)
            {
                Console.WriteLine("Downloading COINS data batch...");
                coinsxmllist = await GetCOINSJsonAsync(_APEXCOINURL);
                Console.WriteLine("COINS batch downloaded.");
                await ProcessCOINSBatch(coinsxmllist);
                moreData = coinsxmllist.Count() >= COINS_ESB.Support.MaxBatchSize ? true : false;
            }

            Console.WriteLine("Process complete.");
#if DEBUG
            Console.ReadLine();
#endif
        }

        private static async Task ProcessCOINSBatch(IEnumerable<CoinsXml> coinsxmllist)
        {
            foreach (var coinsitem in coinsxmllist)
            {
                var xdoc = XDocument.Parse(coinsitem.RawXml);
                var header = xdoc?.Element("COINSInterface")?.Element("Header");
                if (header == null)
                {
                    Console.WriteLine("Unknown document type");
                    await DeleteCOINSIDAsync(_APEXCOINURL, coinsitem.Id);
                    continue;
                }

                var body = xdoc.Element("COINSInterface").Element("Body");
                var documentType = header.Attribute("entity").Value;

                switch (documentType)
                {
                    case "ap_vendor":
                        var vendorInfo = body.Element("ap_vendorRow");
                        await ProcessVendorAsync(vendorInfo);
                        break;
                    case "jc_job":
                        var jobInfo = body.Element("jc_jobRow");
                        await ProcessJobAsync(jobInfo);
                        break;
                    case "jc_wbs":
                        var wbsInfo = body.Element("jc_wbsRow");
                        await ProcessWBSAsync(wbsInfo);
                        break;
                    case "jc_costcode":
                        var ccInfo = body.Element("jc_costcodeRow");
                        await ProcessCCAsync(ccInfo);
                        break;
                    case "se_order": // 4/10/2019 We still don't have any data to examine
                        var woInfo = body.Element("se_orderRow");
                        await ProcessWOAsync(woInfo);
                        break;
                    default:
                        Console.WriteLine($"Unknown document type {documentType}");
                        break;
                }
                await DeleteCOINSIDAsync(_APEXCOINURL, coinsitem.Id); //we're going to just blindly delete (and archive) for now
            }
        }

        private static async Task ProcessCCAsync(XElement ccInfo)
        {
            string apexJobID = ccInfo.Element("job_num").Value.Trim().PadLeft(12);
            if (String.IsNullOrEmpty(apexJobID.Trim())) return;

            string[] jcc_cc = ccInfo.Element("jcc_cc").Value.Split('-');
            if (jcc_cc.Length != 2 && jcc_cc.Length != 3)
            {
                Console.WriteLine($"Unexpected cost code for job {apexJobID}");
                return;
            }

            string apexCostCode;
            string apexPhase = jcc_cc[0];
            if (jcc_cc.Length == 2)
                apexCostCode = jcc_cc[1].PadLeft(9);
            else
                apexCostCode = jcc_cc[2].PadLeft(9);

            using (var dc = new ApexDataDataContext())
            {
                var apexCC = dc.JobPhCcds.Where(v => v.Job == apexJobID && v.Phase == apexPhase && v.CostCode == apexCostCode).SingleOrDefault();
                bool newWBS = (apexCC == null);
                if (newWBS) apexCC = new JobPhCcd();

                apexCC.Job = apexJobID;
                apexCC.Phase = apexPhase;
                apexCC.CostCode = apexCostCode;
                apexCC.Description = LoadValue(ccInfo.Element("jcc_desc").Value, 35);
                apexCC.Act = "A";

                if (newWBS)
                {
                    dc.JobPhCcds.InsertOnSubmit(apexCC);
                    if (!dc.JobPhases.Where(p => p.Job == apexJobID && p.Phase == apexPhase).Any())
                    {
                        JobPhase jp = new JobPhase
                        {
                            Job = apexJobID,
                            Phase = apexPhase,
                            Description = $"Phase {apexPhase}",
                            Act = "A"
                        };
                        dc.JobPhases.InsertOnSubmit(jp);
                    }
                }

                try
                {
                    dc.SubmitChanges();
                    Console.WriteLine($"   Cost Code {apexJobID}, {apexPhase}-{apexCostCode} processed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on Cost Code {apexJobID}, {apexPhase}-{apexCostCode}, {ex}");
                }
            }
        }

        private static async Task ProcessWBSAsync(XElement wbsInfo)
        {
            string apexJobID = wbsInfo.Element("job_num").Value.Trim().PadLeft(12);
            string wbsCode = wbsInfo.Element("jwb_code").Value;
            if (String.IsNullOrEmpty(apexJobID.Trim())) return;

            using (var dc = new ApexDataDataContext())
            {
                var apexWBS = dc.COINSESB_WBs.Where(v => v.Job == apexJobID && v.WB_Code == wbsCode).SingleOrDefault();
                bool newWBS = (apexWBS == null);
                if (newWBS) apexWBS = new COINSESB_WB();

                apexWBS.Job = apexJobID;
                apexWBS.WB_Code = LoadValue(wbsInfo.Element("jwb_code").Value, 50);
                apexWBS.WB_Desc = LoadValue(wbsInfo.Element("jwb_desc").Value, 50);
                apexWBS.Activity = LoadValue(wbsInfo.Element("jca_activity").Value, 50);
                apexWBS.Section = LoadValue(wbsInfo.Element("jcs_section").Value, 50);
                apexWBS.UsesActivity = !String.IsNullOrEmpty(apexWBS.Activity);

                if (newWBS)
                {
                    dc.COINSESB_WBs.InsertOnSubmit(apexWBS);
                }

                try
                {
                    dc.SubmitChanges();
                    Console.WriteLine($"   WBS {apexJobID},{wbsCode} processed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on WBS {apexJobID},{wbsCode}, {ex}");
                }
            }
        }

        private static async Task ProcessJobAsync(XElement jobInfo)
        {
            string apexJobID = jobInfo.Element("job_num").Value.Trim().PadLeft(12);
            if (String.IsNullOrEmpty(apexJobID.Trim())) return;

            using (var dc = new ApexDataDataContext())
            {
                var apexJob = dc.Jobs.Where(v => v.Job1 == apexJobID).SingleOrDefault();
                bool newJob = (apexJob == null);
                if (newJob) apexJob = new Job();

                apexJob.Job1 = apexJobID;
                apexJob.Name = LoadValue(jobInfo.Element("job_name").Value, 25);
                apexJob.Add1 = LoadValue(jobInfo.Element("job_shipaddr__1").Value, 25);
                apexJob.Add2 = LoadValue(jobInfo.Element("job_shipaddr__2").Value, 25);
                apexJob.City = LoadValue(jobInfo.Element("job_shipaddr__3").Value, 15);
                apexJob.State = LoadValue(jobInfo.Element("job_shipaddr__4").Value, 4);
                apexJob.Zip = LoadValue(jobInfo.Element("job_pcode").Value, 15);
                apexJob.Phone = LoadValue(jobInfo.Element("job_tel").Value, 15);

                if (newJob)
                {
                    apexJob.Act = LoadValue(jobInfo.Element("job_active").Value, 1);
                    apexJob.TaxDefault = "N";
                    dc.Jobs.InsertOnSubmit(apexJob);
                }

                try
                {
                    dc.SubmitChanges();
                    Console.WriteLine($"   Job {apexJobID} processed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on Job {apexJobID}, {ex}");
                }
            }
        }

        private static async Task ProcessWOAsync(XElement woInfo)
        {
            string apexWOID = woInfo.Element("sso_order").Value.Trim();
            if (String.IsNullOrEmpty(apexWOID.Trim())) return;

            using (var dc = new ApexDataDataContext())
            {
                var apexWO = dc.WOs.Where(v => v.WO1 == apexWOID).SingleOrDefault();
                bool newWO = (apexWO == null);
                if (newWO) apexWO = new WO();

                apexWO.WO1 = apexWOID;
                apexWO.SiteName = LoadValue(woInfo.Element("slc_name").Value, 25);
                apexWO.SiteAdd1 = LoadValue(woInfo.Element("slc_add__1").Value, 25);
                apexWO.SiteAdd2 = LoadValue(woInfo.Element("slc_add__2").Value, 25);
                apexWO.SiteCity = LoadValue(woInfo.Element("slc_add__3").Value, 15);
                apexWO.SiteState = LoadValue(woInfo.Element("slc_add__4").Value, 4);
                apexWO.SiteZip = LoadValue(woInfo.Element("slc_pcode").Value, 15);
                apexWO.Customer = LoadValue(woInfo.Element("job_num").Value, 4);
                apexWO.Act = "A";

                if (newWO)
                {
                    dc.WOs.InsertOnSubmit(apexWO);
                }

                try
                {
                    dc.SubmitChanges();
                    Console.WriteLine($"   Work Order {apexWOID} processed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on Work Order {apexWOID}, {ex}");
                }
            }
        }

        private async static Task ProcessVendorAsync(XElement vendorInfo)
        {
            string apexVendorID = vendorInfo.Element("avm_num").Value.Trim().PadLeft(6);
            if (String.IsNullOrEmpty(apexVendorID.Trim())) return;

            using (var dc = new ApexDataDataContext())
            {
                var apexVendor = dc.Vendors.Where(v => v.Vendor1 == apexVendorID).SingleOrDefault();
                bool newVendor = (apexVendor == null);

                if (!newVendor)
                {
                    Console.WriteLine($"   Vendor {apexVendorID} is not new, Apex data preserved.");
                    return;
                }

                apexVendor = new Vendor();

                apexVendor.Vendor1 = apexVendorID;
                apexVendor.Name = LoadValue(vendorInfo.Element("avm_name").Value, 25);
                apexVendor.Add1 = LoadValue(vendorInfo.Element("avm_add__1").Value, 25);
                apexVendor.Add2 = LoadValue(vendorInfo.Element("avm_add__2").Value, 25);
                apexVendor.City = LoadValue(vendorInfo.Element("avm_add__3").Value, 15);
                apexVendor.State = LoadValue(vendorInfo.Element("avm_add__4").Value, 4);
                apexVendor.Zip = LoadValue(vendorInfo.Element("avm_pcode").Value, 15);
                apexVendor.Phone = LoadValue(vendorInfo.Element("avm_phone").Value, 15);
                apexVendor.Fax = LoadValue(vendorInfo.Element("avm_fax").Value, 15);
                apexVendor.EMail = LoadValue(vendorInfo.Element("avm_email").Value, 40);

                apexVendor.CompLevel = 0;
                apexVendor.PermitLow = "T";
                apexVendor.AcctID = apexVendorID;
                dc.Vendors.InsertOnSubmit(apexVendor);

                try
                {
                    dc.SubmitChanges();
                    Console.WriteLine($"   Vendor {apexVendorID} processed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on vendor {apexVendorID}, {ex}");
                }
            }
        }

        private static string LoadValue(string value, int maxlen)
        {
            if (value.Length <= maxlen)
                return value;
            else
                return value.Substring(0, maxlen);
        }
    }
}
