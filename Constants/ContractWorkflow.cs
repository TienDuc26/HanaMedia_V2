namespace HanaMedia.Constants;
public static class ContractWorkflow
{
    public static string Label(string? status) => status switch
    {
        "nhap" => "Nháp — chưa gửi duyệt", "cho_duyet" => "Chờ Giám đốc duyệt Booking",
        "da_duyet" => "Đã duyệt — chờ soạn hợp đồng", "cho_phap_ly" => "Chờ Pháp lý kiểm tra",
        "phap_ly_tu_choi" => "Pháp lý yêu cầu sửa", "cho_ky" => "Chờ Giám đốc ký",
        "da_ky" => "Đã ký", "tu_choi" => "Giám đốc từ chối", _ => status ?? "—"
    };
}
