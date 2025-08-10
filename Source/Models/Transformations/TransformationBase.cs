namespace CMMT.Models.Transformations
{
    /// <summary>
    /// Base class for all transformations
    /// </summary>
    public abstract class TransformationBase : ITransformation
    {
        protected Dictionary<string, object> _parameters = new Dictionary<string, object>();

        /// <summary>
        /// Gets the type of this transformation
        /// </summary>
        public abstract TransformationType Type { get; }

        /// <summary>
        /// Transforms a single input value
        /// </summary>
        public abstract string Transform(string input, Dictionary<string, object> parameters);

        /// <summary>
        /// Transforms a single input value with default parameters
        /// </summary>
        public virtual string Transform(string input)
        {
            return Transform(input, _parameters);
        }

        /// <summary>
        /// Gets the current parameters for this transformation
        /// </summary>
        public virtual Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>(_parameters);
        }

        /// <summary>
        /// Transforms a list of sample values
        /// </summary>
        public virtual List<string> TransformSamples(List<string> inputs, Dictionary<string, object> parameters)
        {
            var results = new List<string>();

            foreach (var input in inputs)
            {
                try
                {
                    string result = Transform(input, parameters);
                    results.Add(result);
                }
                catch (Exception)
                {
                    // If transformation fails for a sample, just add a placeholder
                    results.Add("(Error)");
                }
            }

            return results;
        }

        /// <summary>
        /// Gets a user-friendly description of this transformation
        /// </summary>
        public abstract string GetDescription(Dictionary<string, object> parameters);

        /// <summary>
        /// Validates if the parameters are valid for this transformation
        /// </summary>
        public abstract bool ValidateParameters(Dictionary<string, object> parameters, out string error);

        /// <summary>
        /// Helper method to safely get a parameter value with a default
        /// </summary>
        protected T GetParameterValue<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters != null && parameters.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }
    }
}