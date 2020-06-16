//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AIForRentersLib
{
    using System;
    using System.Collections.Generic;
    
    public partial class Request
    {
        public int RequestID { get; set; }
        public System.DateTime FromDate { get; set; }
        public System.DateTime ToDate { get; set; }
        public double PriceUponRequest { get; set; }
        public bool Confirmed { get; set; }
        public int NumberOfPeople { get; set; }
        public string ResponseSubject { get; set; }
        public string ResponseBody { get; set; }
        public int PropertyID { get; set; }
        public int UnitID { get; set; }
        public int ClientID { get; set; }
    
        public virtual Client Client { get; set; }
        public virtual Property Property { get; set; }
        public virtual Unit Unit { get; set; }
    }
}
