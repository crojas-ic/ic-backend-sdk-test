using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;

class Program
{
    private const string BaseUrl = "https://recruitment-test.investcloud.com/api/numbers";
    private const int Size = 1000;

    private static HttpClient _client = new HttpClient();

    static async Task Main(string[] args)
    {
        try
        {

            //start time
            var fetchStopwatch = Stopwatch.StartNew();
            
            // Initialize datasets A and B
            await InitializeDatasets(Size);

            // Retrieve datasets A and B
            var matrixA = await GetMatrix("A");
            var matrixB = await GetMatrix("B");

            // Multiply matrices
            var resultMatrix = MultiplyMatrices(matrixA, matrixB);


            // stop watch
            fetchStopwatch.Stop();
            Console.WriteLine($"Time taken to fetch and multiply datasets: {fetchStopwatch.ElapsedMilliseconds} ms");




            // Create concatenated string from result matrix
            string concatenatedResult = ConcatenateMatrix(resultMatrix);


            // Create MD5 hash of concatenated result
            string hash = ComputeMD5Hash(concatenatedResult);
            //Console.WriteLine(hash);

            // Validate result
            string passphrase = await ValidateResult(hash);
            Console.WriteLine($"Passphrase: {passphrase}");
        }
        catch (Exception err)
        {
            Console.WriteLine($"Error: {err.Message}");
        }
    }

    private static async Task InitializeDatasets(int size)
    {
        var response = await _client.GetAsync($"{BaseUrl}/init/{size}");
        response.EnsureSuccessStatusCode();
    }

   private static async Task<int[,]> GetMatrix(string dataset)
    {
        var matrix = new int[Size, Size];

        
        // get response
        var tasks = new Task<string>[Size];
        for (int i = 0; i < Size; i++)
        {
            int rowIndex = i; 
            tasks[i] = _client.GetStringAsync($"{BaseUrl}/{dataset}/row/{rowIndex}");
        }

        var responses = await Task.WhenAll(tasks);

        // put response into Matrix
        for (int i = 0; i < Size; i++)
        {
            var result = JsonSerializer.Deserialize<ResponseFormat>(responses[i]);

            if (result == null || result.Value == null || result.Value.Length != Size)
            {
                throw new Exception("an error has occured");
            }

            for (int j = 0; j < Size; j++)
            {
                matrix[i, j] = result.Value[j];
            }
        }

        return matrix;

    }


    private static int[,] MultiplyMatrices(int[,] A, int[,] B)
    {
        var result = new int[Size, Size];

        //distribute workload accross multiple threads
        Parallel.For(0, Size, i => 
        {
            for (int j = 0; j < Size; j++)
            {
                result[i, j] = 0;
                for (int k = 0; k < Size; k++)
                {
                    result[i, j] += A[i, k] * B[k, j];
                }
            }
        });

        return result;
    }
    private static string ConcatenateMatrix(int[,] matrix)
    {

       StringBuilder result = new StringBuilder();
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                result.Append(matrix[i, j]);
            }
        }

        return result.ToString(); 
    }


    private static string ComputeMD5Hash(string ConcatM )
    {
        using (MD5 md5 = MD5.Create())
        {

            byte[] inputBytes = Encoding.UTF8.GetBytes(ConcatM);
            
            byte[] hashBytes = md5.ComputeHash(inputBytes);


            //Console.WriteLine(BitConverter.ToString(hashBytes));


            StringBuilder hashBuilder = new StringBuilder();
            foreach (byte i in hashBytes)
            {
                hashBuilder.Append(i);
            }
            string hashString = hashBuilder.ToString();

            //Console.WriteLine(hashString);

            return hashString;
            
            
            // //Console.WriteLine(Convert.ToBase64String(hashBytes));
            //return Encoding.ASCII.GetString(hashBytes);

             //return Encoding.ASCII.GetString(hashBytes);
            //return Convert.ToBase64String(hashBytes);      
        }
    }

    private static async Task<string> ValidateResult(string hash)
    {
       //Console.WriteLine($"MD5 Hash: {hash}");

        var content = new StringContent($"\"{hash}\"", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{BaseUrl}/validate", content);
        response.EnsureSuccessStatusCode();

        // var responseString = await response.Content.ReadAsStringAsync();
        // Console.WriteLine($"Response: {responseString}");

        return await response.Content.ReadAsStringAsync();
    }

    private class ResponseFormat
    {
        public int[] Value { get; set; }
        public string Cause { get; set; }
        public bool Success { get; set; }
    }
}
