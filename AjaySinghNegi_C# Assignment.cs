using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class TimeEntry {
    public string? EmployeeName { get; set; }
    public string? StarTimeUtc { get; set; }
    public string? EndTimeUtc { get; set; }
}

class Program {
	
	// method to generate HTML
    static string GenerateHtmlReport(List<KeyValuePair<string, double>> sortedTime) {
        var html = @"<html>
    <head>
        <title>Employee Work Report</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; background: #f7f7f7; }
            table { border-collapse: collapse; width: 60%; margin: auto; background: #fff; }
            th, td { border: 1px solid #ccc; padding: 10px 15px; text-align: left; }
            th { background-color: #4CAF50; color: white; }
            tr:nth-child(even) { background-color: #f2f2f2; }
            .low-hours { background-color: #ffcccc !important; } /* Highlight <100 hours */
            h2 { text-align: center; }
        </style>
    </head>
    <body>
        <h2>Employee Total Hours Worked</h2>
        <table>
            <tr>
                <th>Employee Name</th>
                <th>Total Hours Worked</th>
            </tr>";

            foreach (var entry in sortedTime) {
                var name = string.IsNullOrEmpty(entry.Key) ? "null" : entry.Key;
                var rowClass = entry.Value < 100 ? "low-hours" : "";
                html += $@"
            <tr class='{rowClass}'>
                <td>{name}</td>
                <td>{entry.Value:F2}</td>
            </tr>";
            }

            html += @"
        </table>
    </body>
</html>";
        return html;
    }
	
	// method to generate PNG pie chart
    static void GeneratePngChart(List<KeyValuePair<string, double>> sortedTime, string pngPath) {
        int width = 1200;
        int height = 1200;
        using (var bitmap = new Bitmap(width, height))
        using (var graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(Color.White);

            var titleFont = new Font("Arial", 32, FontStyle.Bold);
            graphics.DrawString("Employee Total Hours Worked (%)", titleFont, Brushes.Black, new PointF(250, 40));

            double totalHours = 0;
            foreach (var entry in sortedTime) {
                totalHours += entry.Value;
            }

            float startAngle = 0;
            float radius = 450;
            PointF center = new PointF(width / 2, height / 2 + 75);

            var colors = new List<Color> {
                Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Purple, Color.Orange
            };

            int colorIndex = 0;
            foreach (var entry in sortedTime) {
                float sweepAngle = (float)(entry.Value / totalHours) * 360;
                Brush brush = new SolidBrush(colors[colorIndex % colors.Count]);
                graphics.FillPie(brush, center.X - radius, center.Y - radius, 2 * radius, 2 * radius, startAngle, sweepAngle);

                float midAngle = startAngle + sweepAngle / 2;
                double radians = (Math.PI / 180) * midAngle;

                float percentRadius = radius * 0.6f;
                float percentX = center.X + (float)(percentRadius * Math.Cos(radians));
                float percentY = center.Y + (float)(percentRadius * Math.Sin(radians));

                float labelRadius = radius * 1.15f;
                float labelX = center.X + (float)(labelRadius * Math.Cos(radians));
                float labelY = center.Y + (float)(labelRadius * Math.Sin(radians));

                string percentText = $"{(entry.Value / totalHours * 100):0.0}%";
                var percentFont = new Font("Arial", 12, FontStyle.Bold);
                var percentSize = graphics.MeasureString(percentText, percentFont);
                graphics.DrawString(percentText, percentFont, Brushes.Black, percentX - percentSize.Width / 2, percentY - percentSize.Height / 2);

                string name = string.IsNullOrEmpty(entry.Key) ? "null" : entry.Key;
                var labelFont = new Font("Arial", 11, FontStyle.Regular);
                var labelSize = graphics.MeasureString(name, labelFont);
                graphics.DrawString(name, labelFont, Brushes.Black, labelX - labelSize.Width / 2, labelY - labelSize.Height / 2);

                startAngle += sweepAngle;
                colorIndex++;
            }

            bitmap.Save(pngPath, ImageFormat.Png);
        }
    }
	
    static async Task Main(string[] args) {
        // step 1: creating an HttpClient & fetching response
        var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync("https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ=="
        );
        // Console.WriteLine("list of dictionaries: ");
        // Console.WriteLine(response)

        // step 2: data references a list of TimeEntry objects
        var data = JsonSerializer.Deserialize<List<TimeEntry>>(response);
        // Console.WriteLine(data[0].EmployeeName);

        // step 3: creating a dictionary to store total hours worked per employee
        var totalTime = new Dictionary<string, double>();
        foreach (var entry in data) {
            var start = DateTime.Parse(entry.StarTimeUtc);
            var end = DateTime.Parse(entry.EndTimeUtc);
            var duration = end - start;
            // Console.WriteLine(duration);
            // some time durations are negative
            double hours = Math.Abs(duration.TotalHours); 
            // Console.WriteLine(hours);
            // Console.WriteLine(entry.EmployeeName);
            // some of the names are blank. making the empty name as "null" string
            var employeeName = string.IsNullOrEmpty(entry.EmployeeName) ? "null" : entry.EmployeeName;
            // now adding the names with total working hours to the dictionary
            if (totalTime.ContainsKey(employeeName)) {
                totalTime[employeeName] += hours;
            } else {
                totalTime[employeeName] = hours;
            }
        }

        // step 4: sort the above dictionary by hours in descending order
        // creating a list of key value pairs
        var sortedTime = new List<KeyValuePair<string, double>>(totalTime);
        // insertion sort is fast for small data sets
        // currently there are 11 datasets in sortedTime
        for (int i = 1; i < sortedTime.Count; i++) {
            var key = sortedTime[i];
            // Console.WriteLine(key);
            int j = i - 1;
            while (j >= 0 && sortedTime[j].Value < key.Value) {
                sortedTime[j + 1] = sortedTime[j];
                j--;
            }
            sortedTime[j + 1] = key;
        }
        /*
        for (int i=0; i<sortedTime.Count; i++) {
            Console.WriteLine(sortedTime[i]);
        }
        */

        // step 5: creating output directory to store our html and png outputs
        var outputDir = Path.Combine("output");
        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }

        // step 6: Generating an HTML report
        var html = GenerateHtmlReport(sortedTime);
        var htmlPath = Path.Combine(outputDir, "employee_hours.html");
        await File.WriteAllTextAsync(htmlPath, html);
        Console.WriteLine($"{htmlPath} generated successfully.");

        // step 7: Generating a PNG chart
        var pngPath = Path.Combine(outputDir, "employee_hours.png");
        GeneratePngChart(sortedTime, pngPath);
        Console.WriteLine($"{pngPath} generated successfully.");
    }
}
