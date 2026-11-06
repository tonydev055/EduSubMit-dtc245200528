// MepDemo – minh họa 5 mô hình trao đổi thông điệp (MEP) ngay trong một tiến trình.
// Mục đích: thấy "hình dạng" của từng MEP trước khi học công nghệ thật
// (REST, RabbitMQ, SignalR...) ở các bài sau. Không cần package ngoài.
//
// Chạy tất cả:     dotnet run
// Chạy một MEP:    dotnet run -- 3      (1..5)

using System.Threading.Channels;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var only = args.Length > 0 ? args[0] : "all";

if (only is "all" or "1") await Demo1_RequestResponse();
if (only is "all" or "2") await Demo2_OneWay();
if (only is "all" or "3") await Demo3_QueueBased();
if (only is "all" or "4") Demo4_PubSub();
if (only is "all" or "5") await Demo5_Streaming();

// ─────────────────────────────────────────────────────────────
// 1. REQUEST–RESPONSE: gửi yêu cầu và CHỜ phản hồi
//    Công nghệ thật: REST API, GraphQL, gRPC unary (Bài 2, 4, 5)
// ─────────────────────────────────────────────────────────────
static async Task Demo1_RequestResponse()
{
    Title("1. Request–Response: gửi yêu cầu và CHỜ phản hồi");
    var grader = new GradingService();

    var result = await grader.GradeAsync("SV001", latencyMs: 300);
    Console.WriteLine($"[Client] Nhận phản hồi: {result}");

    try
    {
        // Máy chủ chậm hơn thời gian chờ cho phép → máy khách phải tự xử lý timeout
        await grader.GradeAsync("SV002", latencyMs: 2000).WaitAsync(TimeSpan.FromSeconds(1));
    }
    catch (TimeoutException)
    {
        Console.WriteLine("[Client] Quá thời gian chờ 1s → báo lỗi hoặc thử lại sau");
    }
}

// ─────────────────────────────────────────────────────────────
// 2. ONE-WAY: gửi đi rồi làm việc khác, KHÔNG chờ kết quả nghiệp vụ
//    Công nghệ thật: đường ống log, telemetry, message broker
// ─────────────────────────────────────────────────────────────
static async Task Demo2_OneWay()
{
    Title("2. One-way: gửi đi, KHÔNG chờ kết quả nghiệp vụ");
    var logChannel = Channel.CreateUnbounded<string>();

    var auditLog = Task.Run(async () =>
    {
        await foreach (var line in logChannel.Reader.ReadAllAsync())
        {
            await Task.Delay(150);                       // bên nhận xử lý chậm
            Console.WriteLine($"   [AuditLog] đã ghi: {line}");
        }
    });

    for (var i = 1; i <= 3; i++)
    {
        logChannel.Writer.TryWrite($"SV00{i} đã đăng nhập"); // gửi xong là đi tiếp
        Console.WriteLine($"[Sender] gửi log #{i} và tiếp tục việc khác");
    }

    logChannel.Writer.Complete();
    await auditLog; // chỉ để demo kết thúc gọn gàng
}

// ─────────────────────────────────────────────────────────────
// 3. QUEUE-BASED: API đẩy công việc vào hàng đợi, worker lấy ra xử lý.
//    Mỗi công việc chỉ do MỘT worker xử lý (competing consumers).
//    Công nghệ thật: RabbitMQ queue + Worker Service (Bài 6)
// ─────────────────────────────────────────────────────────────
static async Task Demo3_QueueBased()
{
    Title("3. Queue-based: đẩy job vào hàng đợi, worker xử lý dần");
    var queue = Channel.CreateBounded<GradeJob>(capacity: 10);

    async Task Worker(string name)
    {
        await foreach (var job in queue.Reader.ReadAllAsync())
        {
            Console.WriteLine($"   [{name}] bắt đầu chấm {job.SubmissionId}");
            await Task.Delay(400);                        // tác vụ lâu
            Console.WriteLine($"   [{name}] chấm xong {job.SubmissionId}");
        }
    }

    var workers = new[] { Worker("Worker-A"), Worker("Worker-B") };

    for (var i = 1; i <= 4; i++)
    {
        var job = new GradeJob($"sub-{i}");
        await queue.Writer.WriteAsync(job);
        Console.WriteLine($"[API] nhận {job.SubmissionId} → trả ngay status = processing");
    }

    queue.Writer.Complete();
    await Task.WhenAll(workers);
}

// ─────────────────────────────────────────────────────────────
// 4. PUBLISH–SUBSCRIBE: một sự kiện → NHIỀU bên đăng ký độc lập.
//    Bên phát không biết ai nhận, nhận để làm gì.
//    Công nghệ thật: RabbitMQ exchange, Kafka topic, Redis pub/sub (Bài 7)
// ─────────────────────────────────────────────────────────────
static void Demo4_PubSub()
{
    Title("4. Publish–Subscribe: 1 sự kiện → nhiều bên đăng ký");
    var bus = new InMemoryEventBus();

    bus.Subscribe<SubmissionGraded>(e =>
        Console.WriteLine($"   [ScoreService] lưu điểm {e.Score} cho {e.SubmissionId}"));
    bus.Subscribe<SubmissionGraded>(e =>
        Console.WriteLine($"   [NotificationService] báo sinh viên: {e.SubmissionId} đã có điểm"));
    bus.Subscribe<SubmissionGraded>(e =>
        Console.WriteLine("   [StatsService] cập nhật bảng thống kê"));

    Console.WriteLine("[Worker] chấm xong → phát sự kiện SubmissionGraded");
    bus.Publish(new SubmissionGraded("sub-1", 8.5));
}

// ─────────────────────────────────────────────────────────────
// 5. STREAMING / PUSH: máy chủ gửi dữ liệu liên tục, máy khách
//    không phải gửi yêu cầu mới cho mỗi lần cập nhật.
//    Công nghệ thật: SignalR, WebSocket, gRPC stream, SSE (Bài 3)
// ─────────────────────────────────────────────────────────────
static async Task Demo5_Streaming()
{
    Title("5. Streaming / Push: nhận cập nhật liên tục");
    await foreach (var update in WatchSubmission("sub-1"))
    {
        Console.WriteLine($"[Client] cập nhật: {update}");
    }
}

static async IAsyncEnumerable<string> WatchSubmission(string submissionId)
{
    foreach (var status in new[] { "received", "processing 30%", "processing 70%", "graded: 8.5" })
    {
        await Task.Delay(300);
        yield return $"{submissionId} → {status}";
    }
}

static void Title(string text)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 64));
    Console.WriteLine(text);
    Console.WriteLine(new string('=', 64));
}

// ─────────────────────────── Kiểu dữ liệu ───────────────────────────

sealed class GradingService
{
    public async Task<string> GradeAsync(string studentCode, int latencyMs)
    {
        Console.WriteLine($"   [GradingService] nhận yêu cầu chấm bài của {studentCode}");
        await Task.Delay(latencyMs); // giả lập độ trễ mạng + thời gian xử lý
        return $"{studentCode}: 8.5 điểm";
    }
}

sealed class InMemoryEventBus
{
    private readonly Dictionary<Type, List<Action<object>>> _subscribers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = new List<Action<object>>();
            _subscribers[typeof(TEvent)] = handlers;
        }
        handlers.Add(e => handler((TEvent)e));
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers)) return;
        foreach (var handler in handlers) handler(evt); // mỗi subscriber đều nhận
    }
}

record GradeJob(string SubmissionId);

record SubmissionGraded(string SubmissionId, double Score);
