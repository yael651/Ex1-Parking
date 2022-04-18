using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
using System.Collections;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace ParkingLot
{
    public class Function
    {
        public const string TABLE_NAME = "ParkingTable";
        #region public handlers
        public async Task<APIGatewayProxyResponse> AddNewCar(APIGatewayProxyRequest request, ILambdaContext context)
        {
            
            var inputs = request?.QueryStringParameters;
            if (inputs == null) 
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error: empty inputs ..."
                };
            }
            if (inputs.Count != 2)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error: should be 2 arguments  ..."
                };
            }
            string plate = string.Empty;
            string parkingLot = string.Empty;
            foreach(KeyValuePair<string, string> dic in inputs)
            {
                if (dic.Key.Equals("plate"))
                {
                    plate = dic.Value;
                }

                if (dic.Key.Equals("parkingLot"))
                {
                    parkingLot = dic.Value;
                }
            }

            if (string.IsNullOrEmpty(plate))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error, no plate number"
                };
            }
            else if (string.IsNullOrEmpty(parkingLot)) {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error, no prakinglot number"
                };
            } else {
                string newId = await getID();
                await  new AmazonDynamoDBClient().PutItemAsync(new PutItemRequest
                {
                    TableName = TABLE_NAME,
                    Item = new Dictionary<string, AttributeValue>()
                    {
                        { "TicketId", new AttributeValue { S = newId}},
                        { "Plate", new AttributeValue { S = plate }},
                        { "ParkingLot", new AttributeValue { S = parkingLot }},
                        { "Time", new AttributeValue { S = DateTime.Now.ToString() }}
                    }
                });
                return new APIGatewayProxyResponse{
                    StatusCode = 200,
                    Body = string.Format("Ticket ID: {0}.", newId),
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };
            }
        }
        public async Task<APIGatewayProxyResponse> CarExit(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string ticketId = string.Empty;
            var inputs = request?.QueryStringParameters;
            if(inputs == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Erorr no inputs "
                };
            }
            if(!(inputs.TryGetValue("ticketId", out ticketId)))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error no ticketID"
                };
            }
            if(inputs.Count > 1 || inputs.Count == 0)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Error number of args shuld me exactly 1"
                };
            }
            ScanResponse currentList = await GetListAsync();
            foreach (Dictionary<string, AttributeValue> item in currentList.Items)
            {
                AttributeValue ticket = null;
                item.TryGetValue("TicketId", out ticket);
                if (ticket != null)
                {
                    if (ticket.S.Equals(ticketId))
                    {
                        //found the ticketId take out the plate, prakinglot and the time stared
                        AttributeValue plate = null;
                        item.TryGetValue("Plate", out plate);
                        AttributeValue parkId = null;
                        item.TryGetValue("ParkingLot", out parkId);
                        AttributeValue timer = null;
                        item.TryGetValue("Time", out timer);
                        double miniutes = (DateTime.Now - DateTime.Parse(timer.S)).TotalMinutes;
                        string body = string.Format("License Plate: {0}, Total parked time: {1}, Parking lot id: {2}, Charge: {3}", plate.S, string.Format("{0} hours and {1} minutes", Math.Floor(miniutes / 60), String.Format("{0:0.00}", miniutes % 60)), parkId.S, (Math.Ceiling(miniutes /(double)15)*2.5).ToString() + "$");
                        var response = new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = body,
                            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                        };
                        await removeFromDB(ticketId, plate.S);
                        return response;
                    }
                }
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Ticket not found."
            };
        }
        #endregion

        #region private methods

        private async Task removeFromDB(string ticketId, string plate)
        {
            AmazonDynamoDBClient client = new AmazonDynamoDBClient();
            Dictionary<string, AttributeValue> my_dic = new Dictionary<string, AttributeValue>();
            my_dic.Add("TicketId", new AttributeValue() { S = ticketId });
            my_dic.Add("Plate", new AttributeValue() { S = plate });
            await client.DeleteItemAsync(TABLE_NAME, my_dic);
        }
        private async Task<string> getID()
        {
            ScanResponse currentList = await GetListAsync();
            int index = 0;
            bool isFound = false;
            while (!isFound)
            {
                bool res = true;
                foreach (Dictionary<string, AttributeValue> item in currentList.Items)
                {
                    AttributeValue ticket = null;
                    item.TryGetValue("TicketId", out ticket);
                    if (ticket != null)
                    {
                        if (ticket.S.Equals(index.ToString()))
                        {
                            res = false;
                        }
                    }
                }
                if (res)
                {
                    isFound = true;
                }
                else
                {
                    index++;
                }
            }
            return index.ToString();
        }
        private async Task<ScanResponse> GetListAsync()
        {
            ScanResponse response;

            using (var client = new AmazonDynamoDBClient())
            {
                response = await client.ScanAsync(new ScanRequest(TABLE_NAME));
            }

            return (response);
        }
        #endregion
    }
}
