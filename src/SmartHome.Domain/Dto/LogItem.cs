using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHome.Domain.Dto
{
    public class LogItem
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public string Level { get; set; }
        public string Logger { get; set; }
        public string Message { get; set; }
    }
}
