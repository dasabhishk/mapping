using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMMT.Models.Transformations
{
    public class ValueMappingTransformation : TransformationBase
    {
        public override TransformationType Type => TransformationType.ValueMapping;
        public override string Transform(string input, Dictionary<string, object> parameters)
        {
            if (parameters != null && parameters.TryGetValue(input, out var mappedValue) && mappedValue is string s)
                return s;
            return input;
        }
        public override string GetDescription(Dictionary<string, object> parameters)
        {
            return "Maps source values to database values as defined by user mapping.";
        }
        public override bool ValidateParameters(Dictionary<string, object> parameters, out string error)
        {
            error = null;
            // Add validation logic if needed
            return true;
        }
    }
}