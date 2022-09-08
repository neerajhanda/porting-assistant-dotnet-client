using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortingAssistant.Client.Model;

namespace PortingAssistant.Client.Common.Model
{
    public class S3PackageDetailsResponse
    {
        public bool Success { get; private set; }
        public PackageDetails PackageDetails { get; private set; }

        public S3PackageDetailsResponse(bool success, PackageDetails packageDetails = null)
        {
            Success = success;
            PackageDetails = packageDetails;
        }
    }
}
