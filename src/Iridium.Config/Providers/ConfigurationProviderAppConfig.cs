#if NET45
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Iridium.Config
{
    public class ConfigurationProviderAppConfig : IConfigurationProvider
    {
        public long Version()
        {
            return 1;
        }

        public bool CanSave => false;

        public string GetValue(string key, string environment)
        {
            string value = null;

            if (!string.IsNullOrEmpty(environment))
                value = ConfigurationManager.AppSettings[environment + '.' + key];

            return value ?? ConfigurationManager.AppSettings[key];
        }

        public IEnumerable<KeyValuePair<string, string>> EnumerateValues(string key, string environment)
        {
            if (!string.IsNullOrEmpty(environment))
                key = environment + '.' + key;

            key += '.';

            return
                from string s in ConfigurationManager.AppSettings
                where s.StartsWith(key)
                select new KeyValuePair<string, string>(s.Substring(key.Length), ConfigurationManager.AppSettings[s]);
        }


        public void SetValue(string key, string value, string environment)
        {
            throw new NotSupportedException();
        }
    }
}
#endif
