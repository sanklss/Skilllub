namespace SkilllubLearnbox.DTOs
{
    public class LessonDto
    {
        public string Id { get; set; } = "";
        public string ModuleId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Content { get; set; } = "";
        public int Order { get; set; }
        public string Difficulty { get; set; } = "";
        public bool IsCompleted { get; set; } = false; // Добавляем это поле
        public bool HasQuiz { get; set; } = false; // Добавляем это поле
    }
}