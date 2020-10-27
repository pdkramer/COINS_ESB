using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexCOINESB
{
    public class POAmendmentCSVRecord
    {
        public string NewPO { get; set; }
        public string PO { get; set; }
        public string CONum { get; set; }
        public string VariationOrderType { get; set; }
        public string POMajorType { get; set; }
        public string OrderType { get; set; }
        public string HeadOrder { get; set; }
        public string ConfirmationOrder { get; set; }
        public string Job { get; set; }
        public string Account { get; set; }
        public string Currency { get; set; }
        public string Attention { get; set; }
        public string OrderDate { get; set; }
        public string DueDate { get; set; }
        public string Description { get; set; }
        public string Buyer { get; set; }
        public string OrderLineType { get; set; }
        public string Code { get; set; }
        public string ClauseCode { get; set; }
        public string OrderLineDescription { get; set; }
        public string WBSCode { get; set; }
        public string CostCode { get; set; }
        public string CostCategory { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
        public string Price { get; set; }
        public string Per { get; set; }

        public static POAmendmentCSVRecord BuildLine1() =>
            new POAmendmentCSVRecord
            {
                NewPO = "New PO?",
                PO = "PO Number",
                CONum = "CHANGE ORDER NUM",
                VariationOrderType = "Variation Order Type",
                POMajorType = "PO Major Type",
                OrderType = "Order Type",
                HeadOrder = "Head /Site Order?",
                ConfirmationOrder = "Confirmation Order?",
                Job = "Job Number",
                Account = "Account Number",
                Currency = "Currency",
                Attention = "Attention",
                OrderDate = "Order Date",
                DueDate = "Due Date",
                Description = "Description",
                Buyer = "Buyer Code",
                OrderLineType = "Order Line Type",
                Code = "Code",
                ClauseCode = "Clause Code",
                OrderLineDescription = "Order Line Description",
                WBSCode = "WBS Code",
                CostCode = "Cost TYPE",
                CostCategory = "Cost CATEGORY",
                Quantity = "Quantity",
                Unit = "Unit",
                Price = "Price",
                Per = "Per"
            };

        public static POAmendmentCSVRecord BuildLine2() =>
            new POAmendmentCSVRecord
            {
                NewPO = ".new",
                PO = "po_hdr.poh_ordno",
                CONum = "po_hdr.poh_chgno",
                VariationOrderType = "po_hdr.poh_votype",
                POMajorType = "po_hdr.poh_mpo",
                OrderType = "po_hdr.pot_type",
                HeadOrder = "po_hdr.poh_headoffice",
                ConfirmationOrder = "po_hdr.poh_confirm",
                Job = "po_hdr.job_jobph",
                Account = "po_hdr.poh_accno",
                Currency = "po_hdr.cur_code",
                Attention = "po_hdr.poh_attention",
                OrderDate = "po_hdr.poh_odate",
                DueDate = "po_hdr.poh_ddate",
                Description = "po_hdr.poh_desc",
                Buyer = "po_hdr.pob_code",
                OrderLineType = ".addtype",
                Code = "po_line.pol_code",
                ClauseCode = "po_line.tcl_code",
                OrderLineDescription = ".RW_pol_fullDescription",
                WBSCode = "po_line.jwb_code",
                CostCode = "po_line.pol_costhead",
                CostCategory = "po_line.pol_cat",
                Quantity = "po_line.pol_qty",
                Unit = "po_line.pol_uoq",
                Price = "po_line.pol_price",
                Per = "po_line.pol_per"
            };
    }


}
