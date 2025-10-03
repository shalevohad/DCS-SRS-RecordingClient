using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public static class RecordingClientState
    {
        private static SRClientBase _instance;

        public static SRClientBase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SRClientBase
                    {
                        ClientGuid = ShortGuid.NewGuid(),
                        Gateway = true,
                        LineOfSightLoss = 0.0f,
                        Name = "RecordingClient_" + Environment.MachineName,
                    };
                }
                return _instance;
            }
        }

        //allow re-initialization if needed
        public static void Initialize(string clientGuid = "", string name = "")
        {
            _instance = new SRClientBase
            {
                ClientGuid = string.IsNullOrEmpty(clientGuid) ? ShortGuid.NewGuid() : clientGuid,
                Gateway = true,
                LineOfSightLoss = 0.0f,
                Name = string.IsNullOrEmpty(name) ? ("RecordingClient_" + Environment.MachineName) : name,
            };
        }
    }
}