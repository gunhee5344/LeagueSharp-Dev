﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 StringExtensions.cs is part of SFXLibrary.

 SFXLibrary is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXLibrary is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXLibrary. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

namespace SFXLibrary.Extensions.NET
{
    #region

    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml;
    using System.Xml.Serialization;

    #endregion

    public static class StringExtensions
    {
        public static string XmlSerialize<T>(this T objectToSerialise) where T : class
        {
            var serialiser = new XmlSerializer(typeof (T));
            string xml;
            using (var memStream = new MemoryStream())
            {
                using (var xmlWriter = new XmlTextWriter(memStream, Encoding.UTF8))
                {
                    serialiser.Serialize(xmlWriter, objectToSerialise);
                    try
                    {
                        xml = Encoding.UTF8.GetString(memStream.GetBuffer());
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return string.Empty;
                    }
                }
            }
            try
            {
                xml = xml.Substring(xml.IndexOf(Convert.ToChar(60)));
                xml = xml.Substring(0, (xml.LastIndexOf(Convert.ToChar(62)) + 1));
            }
            catch (OverflowException)
            {
                return xml;
            }

            return xml;
        }

        public static T XmlDeserialize<T>(this string xml) where T : class
        {
            var serialiser = new XmlSerializer(typeof (T));
            T newObject = null;

            using (var stringReader = new StringReader(xml))
            {
                using (var xmlReader = new XmlTextReader(stringReader))
                {
                    try
                    {
                        newObject = serialiser.Deserialize(xmlReader) as T;
                    }
                    catch (InvalidOperationException)
                    {
                        return newObject;
                    }
                }
            }
            return newObject;
        }

        public static bool? ToBoolean(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (String.Compare("T", value, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            if (String.Compare("F", value, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return false;
            }
            bool result;
            if (bool.TryParse(value, out result))
            {
                return result;
            }
            return null;
        }

        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
                return string.Empty;

            var truncatedString = value;
            var strLength = maxLength - suffix.Length;
            if (strLength <= 0)
            {
                return truncatedString;
            }
            truncatedString = value.Substring(0, strLength);
            truncatedString = truncatedString.TrimEnd();
            truncatedString += suffix;
            return truncatedString;
        }

        public static string RightSubstring(this string value, int length)
        {
            return value != null && value.Length > length ? value.Substring(value.Length - length) : value;
        }

        public static string LeftSubstring(this string value, int length)
        {
            return value != null && value.Length > length ? value.Substring(0, length) : value;
        }

        public static bool IsNumeric(this string value)
        {
            long retNum;
            return long.TryParse(value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out retNum);
        }

        public static string ToMd5Hash(this string value, bool toLower = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                var originalBytes = Encoding.Default.GetBytes(value);
                var encodedBytes = md5.ComputeHash(originalBytes);
                var stripped = BitConverter.ToString(encodedBytes).Replace("-", string.Empty);
                return toLower ? stripped.ToLower() : stripped;
            }
        }

        public static string ToBase64(this string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        public static string FromBase64(this string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp = StringComparison.Ordinal)
        {
            return !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(toCheck) && source.IndexOf(toCheck, 0, comp) != -1;
        }

        /// <exception cref="OverflowException">
        ///     The array is multidimensional and contains more than
        ///     <see cref="F:System.Int32.MaxValue" /> elements.
        /// </exception>
        public static bool Contains(this string[] source, string toCheck, StringComparison comp = StringComparison.Ordinal)
        {
            if (source.IsNullOrEmpty() || string.IsNullOrEmpty(toCheck))
                return false;

            for (int i = 0, l = source.Length; l > i; i++)
            {
                if (source[i].IndexOf(toCheck, 0, comp) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        public static string Replace(this string value, string[] search, string replace)
        {
            return string.IsNullOrEmpty(value) || search.IsNullOrEmpty()
                ? value
                : search.Aggregate(value, (current, s) => current.Replace(s, replace));
        }

        public static bool IsNullOrEmpty(this string[] value)
        {
            return value == null || value.Length > 0;
        }
    }
}