using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SWCombine.SDK.OAuth;

namespace ConsoleApplication1
{
    class Program
    {
         [STAThread] 
        static void Main(string[] args)
        {
            try
            {
                // dev
                var client_id = "bf25fa7a4cf54ed9138f55964b207fede60dd03f";
                var client_secret = "491d54345ce4308bac9e9e9839587eaf431ebab6";
	 	
                var scopes = new List<string>() { "character_read" };                               

                var swc = SWCombine.SDK.SWC.Initialise(client_id, client_secret);
                swc.AuthoriseComplete += new SWCombine.SDK.AuthoriseCompleteHandler(OnAuthoriseComplete);
                swc.AttemptAuthorise(scopes, "test key;value");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected Error:");
                Console.WriteLine(ex.ToString());
            }
                
            Console.ReadKey();
        }
        
        private static void OnAuthoriseComplete(object sender, AuthoriseCompleteArgs e)
        {
            Console.WriteLine("Authorise Result: " + e.DeniedReason);
        }
    }
}
