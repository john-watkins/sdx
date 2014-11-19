using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Nop.Web.Models.Common
{
    public class JsonResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int errorcode { get; set; }
    }
}