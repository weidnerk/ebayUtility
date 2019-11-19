using dsmodels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eBayUtility
{
    /// <summary>
    /// Used when doing scan.
    /// </summary>
    public class ModelView
    {
        public List<Listing> Listings { get; set; }
        public int TotalOrders { get; set; }
        public double ElapsedSeconds { get; set; }
        public int PercentTotalItemsProcesssed { get; set; }
        public int ReportNumber { get; set; }
        public int ItemCount { get; set; }
    }
}
