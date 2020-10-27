namespace ApexCOINESB
{
    partial class ApexDataDataContext
    {
        partial void InsertCOINSESB_ExpL(COINSESB_ExpL instance)
        {
            instance.PO = instance.PO.Trim().PadLeft(12);
            ExecuteDynamicInsert(instance);
        }

        partial void InsertCOINSESB_WB(COINSESB_WB instance)
        {
            instance.Job = instance.Job.Trim().PadLeft(12);
            ExecuteDynamicInsert(instance);
        }
    }
}