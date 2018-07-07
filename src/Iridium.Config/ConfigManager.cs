﻿#region License
//=============================================================================
// Iridium-Core - Portable .NET Productivity Library 
//
// Copyright (c) 2008-2017 Philippe Leybaert
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//=============================================================================
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Iridium.Reflection;

namespace Iridium.Config
{
    public class ConfigManager
    {
        private string _environment;
        private readonly List<Type> _configTypes = new List<Type>();
        private readonly List<object> _configObjects = new List<object>();
        private List<int> _configVersions = new List<int>();
        private readonly List<IConfigurationProvider> _configProviders = new List<IConfigurationProvider>();

        public static ConfigManager Default { get; } = new ConfigManager();

        public void Register<T>()
        {
            _configTypes.Add(typeof (T));
        }

        public void Register(Type type)
        {
            _configTypes.Add(type);
        }

        public void Register(object obj)
        {
            _configObjects.Add(obj);
        }

        public void RegisterProvider(IConfigurationProvider provider)
        {
            _configProviders.Add(provider);
            _configVersions.Add(0);
        }

        public string Environment
        {
            get => _environment;
            set
            {
                _environment = value;

                // easy way to set all entries to 0
                _configVersions = new List<int>(_configProviders.Count);
                
                Update();
            }
        }

        public bool Update()
        {
            bool updated = false;

            for (int i = 0; i < _configProviders.Count; i++)
            {
                updated |= (_configProviders[i].Version() != _configVersions[i]);
            }

            if (updated)
            {
                foreach (var configType in _configTypes)
                    Fill(configType);

                foreach (var configObject in _configObjects)
                    Fill(configObject);
            }

            return updated;
        }

        private void Fill(Type type)
        {
            ConfigKeyAttribute[] attributes = type.Inspector().GetCustomAttributes<ConfigKeyAttribute>(false);
            
            Fill(type, null, attributes.Length > 0 ? attributes[0].BaseKey : null);
        }

        private void Fill(object obj)
        {
            ConfigKeyAttribute[] attributes = obj.GetType().Inspector().GetCustomAttributes<ConfigKeyAttribute>(false);
            
            Fill(null, obj, attributes.Length > 0 ? attributes[0].BaseKey : null);
        }

        private void Fill(Type type, object obj, string baseKey)
        {
            if (type == null && obj == null)
                throw new ArgumentNullException();

            if (!string.IsNullOrEmpty(baseKey))
                baseKey = baseKey + '.';
            else
                baseKey = "";
            
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.DeclaredOnly;
            
            if (type == null)
            {
                type = obj.GetType();
                bindingFlags |= BindingFlags.Instance;
            }
            else
            {
                bindingFlags |= BindingFlags.Static;
            }

			var members = type.Inspector().GetFieldsAndProperties(bindingFlags);
            
            foreach (var field in members)
            {
                ConfigKeyAttribute[] attributes = field.GetAttributes<ConfigKeyAttribute>(false);

                string key = field.Name;

                if (attributes.Length > 0 && !string.IsNullOrEmpty(attributes[0].BaseKey))
                    key = attributes[0].BaseKey;

                Type fieldType = field.Type;

                bool follow = (attributes.Length > 0 && attributes[0] is ConfigObjectAttribute) || fieldType.Inspector().ImplementsOrInherits<IConfigObject>();
                bool ignore = field.HasAttribute<ConfigIgnoreAttribute>(false);
                
                key = baseKey + key;
                
                if (ignore)
                    continue;

                if (follow)
                {

                    object configObject = field.GetValue(obj);

                    if (configObject == null)
                    {
                        configObject = Activator.CreateInstance(fieldType);

                        field.SetValue(obj, configObject);
                    }

                    Fill(null, configObject, key);
                }
                else
                {
                    if (field.Type.Inspector().ImplementsOrInherits<IDictionary>())
                    {
                        Type dicInterface = fieldType.Inspector().GetInterfaces().FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

                        Type targetType = typeof (object);

                        if (dicInterface != null)
                        {
                            targetType = dicInterface.Inspector().GetGenericArguments()[1];
                        }

                        object configObject = field.GetValue(obj);

                        if (configObject == null)
                        {
                            configObject = Activator.CreateInstance(field.Type);

                            field.SetValue(obj, configObject);
                        }

                        IDictionary dic = (IDictionary) configObject;

                        foreach (var item in GetValues(key))
                        {
                            dic[item.Key] = item.Value.To(targetType);
                        }
                    }
                    else
                    {
                        object value = GetValue(key, field.Type);

                        if (value != null)
                            field.SetValue(obj, value);
                    }
                }
            }
        }

        public object GetValue(string key, Type type)
        {
            return (from provider in _configProviders select provider.GetValue(key, _environment) into value where value != null select value.To(type)).FirstOrDefault();
        }

        public IEnumerable<KeyValuePair<string,string>> GetValues(string key)
        {
            return _configProviders.SelectMany(provider => provider.EnumerateValues(key, _environment));
        }
    }
}