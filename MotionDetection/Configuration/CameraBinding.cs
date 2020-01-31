using System;
using System.Collections.Generic;
using System.Text;

namespace MotionDetection.Configuration
{
    public class CameraBinding
    {
        public string CameraID { get; set; }

        public IEnumerable<CameraDirectionLightStateBinding> Bindings { get; set; }
    }
}
