﻿namespace Certes.Cli.Options
{
    internal class AccountOptions : OptionsBase
    {
        public AccountAction Action;
        public string Email = "";
        public bool AgreeTos = false;
    }
}
