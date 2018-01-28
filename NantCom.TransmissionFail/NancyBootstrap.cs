using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Nancy;
using Nancy.Conventions;

namespace NantCom.TransmissionFail
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            base.ConfigureConventions(nancyConventions);
            nancyConventions.StaticContentsConventions.AddDirectory("Scripts");
            nancyConventions.StaticContentsConventions.AddDirectory("Content");
            nancyConventions.StaticContentsConventions.AddDirectory("Js");
        }
    }
}