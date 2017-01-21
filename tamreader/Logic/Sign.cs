using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tamreader.Logic
{
   public class Sign
    {
        private String name;
        private int obeyTimes;
        private int disobeyTimes;

        public Sign() {


        }
        public Sign(Sign sign)
        {
            this.name = sign.Name;
            this.obeyTimes = sign.obeyTimes;
            this.disobeyTimes = sign.DisobeyTimes;
        }

        /// <summary>
        /// The Sign's name.
        /// </summary>
        public String Name {
            get { return name; }
            set { name = value; }
           }

        /// <summary>
        /// The Sign's Obedience times. 
        /// </summary>
        public int ObeyTimes
        {
            get { return obeyTimes; }
            set { obeyTimes = value; }
        }


        /// <summary>
        /// The Sign's DisObedience times. 
        /// </summary>
        public int DisobeyTimes
        {
            get { return disobeyTimes; }
            set { disobeyTimes = value; }
        }
    }
}
