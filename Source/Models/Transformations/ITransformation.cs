namespace CMMT.Models.Transformations
{
    //<summary>
    //Interface for all data transformations
    //</summary>
    public interface ITransformation
    {

        //Gets the type of this transformation

        TransformationType Type { get; }

        string Transform(string input, Dictionary<string, object> parameters);


        //Transforms a single input value with default parameters


        string Transform(string input);


        //Transforms a list of sample values

        List<string> TransformSamples(List<string> inputs, Dictionary<string, object> parameters);
        //Gets the current parameters for this transformation

        //<returns>Dictionary of parameters</returns>
        Dictionary<string, object> GetParameters();

        //Gets a user-friendly description of this transformation

        string GetDescription(Dictionary<string, object> parameters);

        //Validates if the parameters are valid for this transformation

        bool ValidateParameters(Dictionary<string, object> parameters, out string error);
    }
}