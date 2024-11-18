using Autodesk.Navisworks.Api;
using System.Text;
using System.Windows.Forms;

namespace AddinRibbon
{
    public class ApiSearchObject
    {
        public static void SearchObject(string inputValue)
        {
            //Create a new search
            Search s = new Search();
            //set the selection to everything
            //Add a search condition
            s.SearchConditions.Add(SearchCondition.HasCategoryByDisplayName(inputValue));

            var sc = new SearchCondition(
                new NamedConstant("LcOaNode", "Item"),
                 new NamedConstant("LcOaSceneBaseUserName", "Name"),
                SearchConditionOptions.StartGroup,
                 SearchConditionComparison.Equal,
                 VariantData.FromBoolean(false)
                );

            s.SearchConditions.Add(sc);

            s.Selection.SelectAll();
            s.Locations = SearchLocations.DescendantsAndSelf;

            s.PruneBelowMatch = true;

            ModelItemCollection searchResults = s.FindAll(Autodesk.Navisworks.Api.Application.ActiveDocument, false);

            StringBuilder output = new StringBuilder();
            output.AppendLine("Found the following:");

            //show the results
            foreach (ModelItem mi in searchResults)
            {
                output.AppendLine(mi.ToString());
            }
            MessageBox.Show(output.ToString());
        }
    }
}

