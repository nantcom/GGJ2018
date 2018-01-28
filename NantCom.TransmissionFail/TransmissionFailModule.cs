using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NantCom.TransmissionFail
{
    public class TransmissionFailModule : Nancy.NancyModule
    {
        public TransmissionFailModule()
        {
            Get["/"] = (arg)=>
            {
                return View["Game"];
            };
        }
    }
}