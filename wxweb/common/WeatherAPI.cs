using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace wxweb
{
        public class location
        {
            public string id { get; set; }
            public string name { get; set; }
            public string country { get; set; }
            public string path { get; set; }
            public string timezone { get; set; }
            public string timezone_offset { get; set; }
        }

        public class now
        {
            public string text { get; set; }
            public string code { get; set; }
            public string temperature { get; set; }
        }
        public class results
        {

            public string last_update { get; set; }
            public location location { get; set; }
            public now now { get; set; }
        }

        public class weather
        {
            public List<results> results;
        }

   
    
}