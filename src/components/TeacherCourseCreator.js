export class TeacherCourseCreator {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentCourseId = null;
        this.modules = [];
    }

    initialize() {
        this.setupEventListeners();
    }

    setupEventListeners() {
        document.getElementById('create-course-btn')?.addEventListener('click', () => {
            this.showCourseCreationModal();
        });

        document.getElementById('add-module-btn')?.addEventListener('click', () => {
            this.addModule();
        });

        document.getElementById('save-course-template')?.addEventListener('click', () => {
            this.saveCourseTemplate();
        });

        document.getElementById('publish-course-btn')?.addEventListener('click', () => {
            this.publishCourse();
        });
    }

    showCourseCreationModal() {
        const modal = document.getElementById('modal-create-course');
        if (!modal) {
            this.createCourseModal();
        } else {
            modal.classList.remove('hidden');
        }
        
        this.modules = [{ 
            title: '', 
            lessonsCount: 1,
            lessons: [{ title: '', hasQuiz: false, hasCode: false }]
        }];
        this.renderModulesList();
    }

    createCourseModal() {
        const modal = document.createElement('div');
        modal.id = 'modal-create-course';
        modal.className = 'modal hidden';
        
        modal.innerHTML = `
            <div class="modal-card" style="max-width: 800px; width: 90%; max-height: 80vh; overflow-y: auto;">
                <header style="display: flex; justify-content: space-between; align-items: center; padding: 15px 20px; border-bottom: 1px solid #e2e8f0;">
                    <h3 style="margin: 0;">Создание нового курса</h3>
                    <button class="close-btn" onclick="document.getElementById('modal-create-course').classList.add('hidden')">✕</button>
                </header>
                
                <div class="modal-content" style="padding: 20px;">
                    <form id="create-course-form">
                        <div class="form-group" style="margin-bottom: 20px;">
                            <label style="display: block; margin-bottom: 5px; font-weight: 500;">Название курса *</label>
                            <input type="text" id="course-title" class="form-input" style="width: 100%; padding: 8px; border: 1px solid #e2e8f0; border-radius: 5px;" required>
                        </div>
                        
                        <div class="form-group" style="margin-bottom: 20px;">
                            <label style="display: block; margin-bottom: 5px; font-weight: 500;">Описание</label>
                            <textarea id="course-description" class="form-input" rows="3" style="width: 100%; padding: 8px; border: 1px solid #e2e8f0; border-radius: 5px;"></textarea>
                        </div>
                        
                        <div class="form-group" style="margin-bottom: 20px;">
                            <label style="display: block; margin-bottom: 5px; font-weight: 500;">Уровень сложности</label>
                            <select id="course-difficulty" class="filter-select" style="width: 100%; padding: 8px;">
                                <option value="beginner">Начальный</option>
                                <option value="intermediate">Средний</option>
                                <option value="advanced">Продвинутый</option>
                            </select>
                        </div>
                        
                        <div class="modules-container" id="modules-container" style="margin-bottom: 20px;">
                            <h4 style="margin: 0 0 15px 0;">Модули и уроки</h4>
                            <div id="modules-list"></div>
                            <button type="button" id="add-module-btn" class="btn-secondary" style="margin-top: 10px;">
                                + Добавить модуль
                            </button>
                        </div>
                        
                        <div class="modal-actions" style="display: flex; gap: 10px; justify-content: flex-end; margin-top: 20px;">
                            <button type="button" id="save-course-template" class="btn-primary">Создать черновик</button>
                            <button type="button" class="btn-secondary" onclick="document.getElementById('modal-create-course').classList.add('hidden')">Отмена</button>
                        </div>
                    </form>
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        modal.classList.remove('hidden');
        
        document.getElementById('add-module-btn').addEventListener('click', () => this.addModule());
        document.getElementById('save-course-template').addEventListener('click', () => this.saveCourseTemplate());
        
        this.modules = [{ 
            title: '', 
            lessonsCount: 1,
            lessons: [{ title: '', hasQuiz: false, hasCode: false }]
        }];
        this.renderModulesList();
    }

    renderModulesList() {
        const container = document.getElementById('modules-list');
        if (!container) return;
        
        let html = '';
        
        this.modules.forEach((module, moduleIndex) => {
            html += `
                <div class="module-card" style="border: 1px solid #e2e8f0; border-radius: 8px; margin-bottom: 20px; padding: 15px; background: #f8fafc;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;">
                        <h5 style="margin: 0;">Модуль ${moduleIndex + 1}</h5>
                        ${this.modules.length > 1 ? 
                            `<button type="button" class="btn-danger btn-sm" onclick="app.teacherCourseCreator.removeModule(${moduleIndex})">Удалить</button>` : 
                            ''}
                    </div>
                    
                    <div class="form-group" style="margin-bottom: 15px;">
                        <label style="display: block; margin-bottom: 5px; font-size: 14px;">Название модуля</label>
                        <input type="text" class="form-input module-title" data-index="${moduleIndex}" 
                               value="${module.title}" style="width: 100%; padding: 8px; border: 1px solid #e2e8f0; border-radius: 5px;">
                    </div>
                    
                    <div class="lessons-container" id="lessons-${moduleIndex}">
                        ${this.renderLessons(module.lessons, moduleIndex)}
                    </div>
                    
                    <button type="button" class="btn-secondary btn-sm" onclick="app.teacherCourseCreator.addLesson(${moduleIndex})" style="margin-top: 10px;">
                        + Добавить урок
                    </button>
                </div>
            `;
        });
        
        container.innerHTML = html;
        
        document.querySelectorAll('.module-title').forEach(input => {
            input.addEventListener('input', (e) => {
                const index = parseInt(e.target.dataset.index);
                this.modules[index].title = e.target.value;
            });
        });
        
        this.modules.forEach((module, moduleIndex) => {
            module.lessons.forEach((lesson, lessonIndex) => {
                const titleInput = document.getElementById(`lesson-title-${moduleIndex}-${lessonIndex}`);
                if (titleInput) {
                    titleInput.addEventListener('input', (e) => {
                        this.modules[moduleIndex].lessons[lessonIndex].title = e.target.value;
                    });
                }
                
                const quizCheckbox = document.getElementById(`lesson-quiz-${moduleIndex}-${lessonIndex}`);
                if (quizCheckbox) {
                    quizCheckbox.addEventListener('change', (e) => {
                        this.modules[moduleIndex].lessons[lessonIndex].hasQuiz = e.target.checked;
                    });
                }
                
                const codeCheckbox = document.getElementById(`lesson-code-${moduleIndex}-${lessonIndex}`);
                if (codeCheckbox) {
                    codeCheckbox.addEventListener('change', (e) => {
                        this.modules[moduleIndex].lessons[lessonIndex].hasCode = e.target.checked;
                    });
                }
            });
        });
    }

    renderLessons(lessons, moduleIndex) {
        let html = '<h6 style="margin: 10px 0;">Уроки:</h6>';
        
        lessons.forEach((lesson, lessonIndex) => {
            html += `
                <div class="lesson-row" style="display: flex; align-items: center; gap: 10px; margin-bottom: 10px; padding: 10px; background: white; border-radius: 5px;">
                    <div style="flex: 2;">
                        <input type="text" id="lesson-title-${moduleIndex}-${lessonIndex}" 
                               placeholder="Название урока" value="${lesson.title}"
                               style="width: 100%; padding: 6px; border: 1px solid #e2e8f0; border-radius: 4px;">
                    </div>
                    
                    <div style="flex: 3; display: flex; gap: 15px;">
                        <label style="display: flex; align-items: center; gap: 5px;">
                            <input type="checkbox" id="lesson-quiz-${moduleIndex}-${lessonIndex}" 
                                   ${lesson.hasQuiz ? 'checked' : ''}>
                            <span>📝 Тест</span>
                        </label>
                        
                        <label style="display: flex; align-items: center; gap: 5px;">
                            <input type="checkbox" id="lesson-code-${moduleIndex}-${lessonIndex}" 
                                   ${lesson.hasCode ? 'checked' : ''}>
                            <span>💻 Код</span>
                        </label>
                        
                        <span style="color: #94a3b8; display: flex; align-items: center;">
                            <span>📖 Теория</span>
                        </span>
                    </div>
                    
                    ${lessons.length > 1 ? 
                        `<button type="button" class="btn-danger btn-xs" onclick="app.teacherCourseCreator.removeLesson(${moduleIndex}, ${lessonIndex})">✕</button>` : 
                        ''}
                </div>
            `;
        });
        
        return html;
    }

    addModule() {
        this.modules.push({ 
            title: '', 
            lessonsCount: 1,
            lessons: [{ title: '', hasQuiz: false, hasCode: false }]
        });
        this.renderModulesList();
    }

    removeModule(index) {
        this.modules.splice(index, 1);
        this.renderModulesList();
    }

    addLesson(moduleIndex) {
        this.modules[moduleIndex].lessons.push({ title: '', hasQuiz: false, hasCode: false });
        this.renderModulesList();
    }

    removeLesson(moduleIndex, lessonIndex) {
        this.modules[moduleIndex].lessons.splice(lessonIndex, 1);
        this.renderModulesList();
    }

    async saveCourseTemplate() {
        const title = document.getElementById('course-title').value;
        if (!title) {
            this.uiManager.showToast('Введите название курса', 'warning');
            return;
        }

        for (let i = 0; i < this.modules.length; i++) {
            for (let j = 0; j < this.modules[i].lessons.length; j++) {
                if (!this.modules[i].lessons[j].title) {
                    this.uiManager.showToast(`Заполните название урока ${j+1} в модуле ${i+1}`, 'warning');
                    return;
                }
            }
        }

        const courseData = {
            title: title,
            description: document.getElementById('course-description').value,
            difficultyLevel: document.getElementById('course-difficulty').value,
            modulesCount: this.modules.length,
            modules: this.modules.map((module, index) => ({
                title: module.title || `Модуль ${index + 1}`,
                order: index + 1,
                lessonsCount: module.lessons.length,
                lessons: module.lessons.map((lesson, lessonIndex) => ({
                    title: lesson.title,
                    order: lessonIndex + 1,
                    hasTheory: true,
                    hasQuiz: lesson.hasQuiz,
                    hasCode: lesson.hasCode
                }))
            }))
        };

        try {
            this.uiManager.showButtonLoading('save-course-template', true);
            
            const response = await fetch('/api/teacher/courses/create-template', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                },
                body: JSON.stringify(courseData)
            });

            const result = await response.json();
            
            if (result.success) {
                this.currentCourseId = result.course.courseId;
                this.uiManager.showToast('Черновик курса создан! Теперь можно заполнить уроки.', 'success');
                document.getElementById('modal-create-course').classList.add('hidden');
                
                this.showCourseEditor(this.currentCourseId);
            } else {
                throw new Error(result.error || 'Ошибка создания курса');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('save-course-template', false);
        }
    }

    async showCourseEditor(courseId) {
        try {
            const response = await fetch(`/api/teacher/courses/${courseId}/structure`, {
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.renderCourseEditor(result.structure);
            }
        } catch (error) {
            console.error('Ошибка загрузки структуры:', error);
        }
    }

    renderCourseEditor(structure) {
        let modal = document.getElementById('modal-course-editor');
        
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'modal-course-editor';
            modal.className = 'modal';
            document.body.appendChild(modal);
        }

        
        modal.innerHTML = `
            <div class="modal-card" style="max-width: 1000px; width: 95%; max-height: 90vh; overflow-y: auto;">
                <header style="display: flex; justify-content: space-between; align-items: center; padding: 15px 20px; border-bottom: 1px solid #e2e8f0;">
                    <h3 style="margin: 0;">Редактирование курса: ${structure.course.title}</h3>
                    <button class="close-btn" onclick="document.getElementById('modal-course-editor').classList.add('hidden')">✕</button>
                </header>
                
                <div class="modal-content" style="padding: 20px;">
                    <div style="margin-bottom: 20px;">
                        <button class="btn-success" id="publish-course-btn">Опубликовать курс</button>
                    </div>
                    
                    <div class="course-structure">
                        ${this.renderCourseModules(structure)}
                    </div>
                </div>
            </div>
        `;
        
        document.getElementById('publish-course-btn').addEventListener('click', () => this.publishCourse());
        
        modal.classList.remove('hidden');
    }

    renderCourseModules(structure) {
        let html = '';
        
        structure.modules.forEach((module, moduleIndex) => {
            html += `
                <div class="editor-module" style="border: 1px solid #e2e8f0; border-radius: 8px; margin-bottom: 20px; background: #f8fafc;">
                    <div style="padding: 15px; border-bottom: 1px solid #e2e8f0; background: #f1f5f9; border-radius: 8px 8px 0 0;">
                        <h4 style="margin: 0;">${module.title}</h4>
                    </div>
                    
                    <div style="padding: 15px;">
                        ${module.lessons.map((lesson, lessonIndex) => `
                            <div class="editor-lesson" style="border: 1px solid #e2e8f0; border-radius: 5px; margin-bottom: 15px; background: white;">
                                <div style="padding: 10px 15px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center;">
                                    <h5 style="margin: 0;">${lesson.title}</h5>
                                    <div style="display: flex; gap: 10px;">
                                        ${lesson.hasQuiz ? '<span class="badge" style="background: #3b82f6; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px;">📝 Тест</span>' : ''}
                                        ${lesson.hasCode ? '<span class="badge" style="background: #10b981; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px;">💻 Код</span>' : ''}
                                        <span class="badge" style="background: #64748b; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px;">📖 Теория</span>
                                    </div>
                                </div>
                                
                                <div style="padding: 15px;">
                                    <button class="btn-secondary btn-sm" onclick="app.teacherCourseCreator.editLesson('${structure.course.id}', '${lesson.id}', ${lesson.hasQuiz}, ${lesson.hasCode})">
                                        Редактировать урок
                                    </button>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
            `;
        });
        
        return html;
    }

    async editLesson(courseId, lessonId, hasQuiz, hasCode) {

        
        console.log('Редактирование урока:', { courseId, lessonId, hasQuiz, hasCode });
        
        this.uiManager.showToast('Редактор урока в разработке', 'info');
    }

    async publishCourse() {
        if (!this.currentCourseId) return;
        
        if (!confirm('Опубликовать курс? После публикации курс станет доступен для студентов.')) return;
        
        try {
            const response = await fetch(`/api/teacher/courses/${this.currentCourseId}/publish`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                },
                body: JSON.stringify({ courseId: this.currentCourseId })
            });

            const result = await response.json();
            
            if (result.success) {
                this.uiManager.showToast('Курс успешно опубликован!', 'success');
                document.getElementById('modal-course-editor').classList.add('hidden');
                
                if (window.app && window.app.teacherManager) {
                    window.app.teacherManager.loadTeacherCourses();
                }
            } else {
                throw new Error(result.error || 'Ошибка публикации');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast(error.message, 'error');
        }
    }
}