// EduSubmit – Submission.Client (vai trò MÁY KHÁCH)
// Chạy: dotnet run --project src/Submission.Client -- http://localhost:5000

using System.Net.Http.Json;

// Địa chỉ API lấy từ tham số dòng lệnh; không có thì dùng mặc định
var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

using var http = new HttpClient {
    BaseAddress = new Uri(apiBaseUrl)
};

// Yêu cầu 1: GET /api/assignments
var assignments = await http.GetFromJsonAsync<List<Assignment>>("/api/assignments");
Console.WriteLine("Danh sach bai tap:");

foreach (var assignment in assignments ?? new List<Assignment>()) {
    Console.WriteLine($"- {assignment.Id}: {assignment.Title}");
}

// Yêu cầu 2: POST /api/submissions
var request = new CreateSubmissionRequest(
    AssignmentId: 1,
    StudentCode: "SV001",
    RepositoryUrl: "https://example.com/sv001/bai-tap-1");

var response = await http.PostAsJsonAsync("/api/submissions", request);
response.EnsureSuccessStatusCode();   // kiểm tra mã trạng thái trước khi đọc kết quả

var result = await response.Content.ReadFromJsonAsync<SubmissionResult>();
Console.WriteLine($"Da nop bai. Ma luot nop: {result?.Id}");
Console.WriteLine($"Trang thai: {result?.Status}");

// Client tự khai báo lại hợp đồng dữ liệu, KHÔNG dùng chung code với API
record Assignment(int Id, string Title, string Description);
record CreateSubmissionRequest(int AssignmentId, string StudentCode, string RepositoryUrl);
record SubmissionResult(Guid Id, int AssignmentId, string StudentCode,
    string Status, DateTimeOffset ReceivedAt);
