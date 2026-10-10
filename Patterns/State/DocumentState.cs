using System;

namespace QuanLyKho.Patterns.State
{
    public interface IDocumentState
    {
        string Name { get; }
        bool CanEdit { get; }
        bool CanComplete { get; }
        bool CanCancel { get; }
        void ValidateTransition(string targetState);
    }

    public class DraftState : IDocumentState
    {
        public string Name => "Draft";
        public bool CanEdit => true;
        public bool CanComplete => true;
        public bool CanCancel => true;

        public void ValidateTransition(string targetState)
        {
            if (targetState != "Completed" && targetState != "Cancelled")
            {
                throw new InvalidOperationException($"Từ trạng thái 'Nháp' (Draft) chỉ có thể chuyển sang 'Hoàn tất' (Completed) hoặc 'Đã hủy' (Cancelled).");
            }
        }
    }

    public class CompletedState : IDocumentState
    {
        public string Name => "Completed";
        public bool CanEdit => false;
        public bool CanComplete => false;
        public bool CanCancel => false;

        public void ValidateTransition(string targetState)
        {
            // BR.05, BR.06, BR.07: Chứng từ hoàn tất không được sửa chi tiết hoặc hoàn tất lặp lại
            throw new InvalidOperationException("Chứng từ đã 'Hoàn tất' (Completed) không được phép chỉnh sửa hoặc chuyển trạng thái nữa.");
        }
    }

    public class CancelledState : IDocumentState
    {
        public string Name => "Cancelled";
        public bool CanEdit => false;
        public bool CanComplete => false;
        public bool CanCancel => false;

        public void ValidateTransition(string targetState)
        {
            throw new InvalidOperationException("Chứng từ đã 'Hủy' (Cancelled) không thể thay đổi trạng thái.");
        }
    }

    public static class DocumentStateFactory
    {
        public static IDocumentState GetState(string? status)
        {
            return status?.ToLower() switch
            {
                "completed" or "hoantat" => new CompletedState(),
                "cancelled" or "dahuy" => new CancelledState(),
                _ => new DraftState()
            };
        }
    }
}
