using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AKM.Gearbox.MES.Desktop.Models
{
    public class Order
    {
        public string OrderId { get; set; }        // Örn: 1000.1, 1000.2
        public string OrderDesc { get; set; }
        public DateTime OrderCreationDate { get; set; }
        public string OrderType { get; set; }
        public string OrderStatus { get; set; }     // Örn: New, In Progress, Completed
        public string CreatedBy { get; set; }
        public bool IsProcessed { get; set; }
    }
}