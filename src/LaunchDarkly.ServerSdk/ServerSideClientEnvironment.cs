﻿using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class ServerSideClientEnvironment : ClientEnvironment
    {
        internal static readonly ServerSideClientEnvironment Instance =
            new ServerSideClientEnvironment();
        
        public override string UserAgentType { get { return "DotNetClient";  } }
    }
}
