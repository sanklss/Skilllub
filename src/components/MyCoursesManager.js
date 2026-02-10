export class MyCoursesManager {
    constructor(apiService, uiManager, courseManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.courseManager = courseManager;
    }

    async loadMyCourses() {
        try {
            const result = await this.api.getMyCourses();
            if (result.success) {
                this.renderMyCourses(result.courses);
            } else {
                this.renderEmptyState();
            }
        } catch (error) {
            console.error('Ошибка загрузки курсов:', error);
            this.renderEmptyState();
        }
    }

    renderMyCourses(courses) {
        const container = document.getElementById('my-courses-container');
        if (!container) return;

        if (courses.length === 0) {
            this.renderEmptyState();
            return;
        }

        let html = `
            <div class="my-courses-header">
                <h2>Мои курсы (${courses.length})</h2>
                <p class="subtitle">Продолжайте обучение с того места, где остановились</p>
            </div>
            <div class="my-courses-grid">
        `;

        courses.forEach(course => {
            const progressClass = course.progress >= 100 ? 'completed' : 
                                course.progress >= 50 ? 'in-progress' : 'started';
            
            html += `
                <div class="my-course-card ${progressClass}">
                    <div class="course-card-header">
                        <h3 class="course-title">${course.title}</h3>
                        <span class="progress-badge">${course.progress}%</span>
                    </div>
                    
                    <div class="course-description">
                        ${course.description || 'Описание курса не указано'}
                    </div>
                    
                    <div class="progress-container">
                        <div class="progress-bar">
                            <div class="progress-fill" style="width: ${course.progress}%"></div>
                        </div>
                        <div class="progress-text">${course.progress}% завершено</div>
                    </div>
                    
                    <div class="course-meta">
                        <div class="meta-item">
                            <span class="meta-label">Записан:</span>
                            <span class="meta-value">${new Date(course.enrolledAt).toLocaleDateString()}</span>
                        </div>
                        ${course.lastAccessed ? `
                        <div class="meta-item">
                            <span class="meta-label">Последний доступ:</span>
                            <span class="meta-value">${new Date(course.lastAccessed).toLocaleDateString()}</span>
                        </div>
                        ` : ''}
                    </div>
                    
                    <div class="course-actions">
                        <button class="btn-primary" onclick="app.myCoursesManager.openCourse('${course.courseId}')">
                            ${course.completed ? 'Повторить курс' : 'Продолжить обучение'}
                        </button>
                        ${course.completed ? 
                            '<span class="completion-badge">✓ Завершен</span>' : 
                            '<span class="in-progress-badge">В процессе</span>'
                        }
                    </div>
                </div>
            `;
        });

        html += '</div>';
        container.innerHTML = html;
    }

    renderEmptyState() {
        const container = document.getElementById('my-courses-container');
        if (!container) return;

        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-icon">📚</div>
                <h3>У вас пока нет активных курсов</h3>
                <p>Выберите курс из каталога и запишитесь на него, чтобы начать обучение</p>
                <div class="empty-actions">
                    <button class="btn-primary" onclick="app.uiManager.showSection('catalog')">
                        Перейти в каталог курсов
                    </button>
                    <button class="btn-secondary" onclick="app.uiManager.showSection('catalog')">
                        Посмотреть все курсы
                    </button>
                </div>
            </div>
        `;
    }

    async openCourse(courseId) {
        await this.courseManager.openCourse(courseId);
    }
}