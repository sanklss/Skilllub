// src/components/QuizManager.js
export class QuizManager {
    constructor(apiService, uiManager, authManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.authManager = authManager;
        this.currentQuizQuestions = [];
        this.currentQuestionIndex = 0;
        this.userAnswers = {};
        this.isQuizSubmitted = false;
        this.correctAnswersCount = 0;
    }

    initialize() {
        this.setupQuizEventListeners();
    }

    setupQuizEventListeners() {
        document.getElementById('quiz-submit')?.addEventListener('click', () => this.submitQuiz());
        document.getElementById('quiz-close')?.addEventListener('click', () => this.closeQuiz());
    }

    async loadQuizQuestions(lessonId) {
        try {
            console.log('Loading quiz questions for lesson:', lessonId);
            
            const result = await this.api.getQuizQuestions(lessonId);
            
            if (result.success && result.questions && result.questions.length > 0) {
                console.log('Quiz questions loaded:', result.questions.length);
                
                this.currentQuizQuestions = result.questions;
                this.currentQuestionIndex = 0;
                this.userAnswers = {};
                this.isQuizSubmitted = false;
                this.correctAnswersCount = 0;
                
                this.uiManager.showQuizSection();
                this.renderQuiz();
                return true;
            } else {
                console.log('No quiz questions found for this lesson');
                this.uiManager.hideQuizSection();
                return false;
            }
        } catch (error) {
            console.error('Failed to load quiz questions:', error);
            this.uiManager.hideQuizSection();
            return false;
        }
    }

    renderQuiz() {
        const quizContainer = document.getElementById('quiz-section');
        if (!quizContainer) return;

        quizContainer.innerHTML = `
            <div class="quiz-header">
                <h3>Проверка знаний</h3>
                <button class="btn-secondary btn-sm" id="quiz-close">✕</button>
            </div>
            
            <div class="quiz-questions">
                ${this.currentQuizQuestions.map((question, index) => `
                    <div class="quiz-question" data-question-id="${question.id}">
                        <div class="question-text">
                            <strong>Вопрос ${index + 1}:</strong> ${question.questionText}
                        </div>
                        <div class="quiz-options">
                            ${[1, 2, 3, 4].map(optionNum => `
                                <label class="quiz-option">
                                    <input type="radio" 
                                           name="question_${question.id}" 
                                           value="${optionNum}"
                                           ${this.isQuizSubmitted && optionNum === question.correctOption ? 'disabled' : ''}
                                           ${this.userAnswers[question.id] === optionNum ? 'checked' : ''}>
                                    <span class="option-text">${question[`option${optionNum}`]}</span>
                                </label>
                            `).join('')}
                        </div>
                        ${this.isQuizSubmitted ? `
                            <div class="quiz-result ${this.userAnswers[question.id] === question.correctOption ? 'correct' : 'incorrect'}">
                                ${this.userAnswers[question.id] === question.correctOption ? 
                                    '✓ Правильно' : 
                                    `✗ Неправильно. Правильный ответ: ${question[`option${question.correctOption}`]}`}
                                ${question.explanation ? `<div class="explanation">${question.explanation}</div>` : ''}
                            </div>
                        ` : ''}
                    </div>
                `).join('')}
            </div>
            
            <div class="quiz-actions">
                ${!this.isQuizSubmitted ? `
                    <button class="btn-primary" id="quiz-submit">
                        Проверить решение
                    </button>
                ` : `
                    <div class="quiz-summary">
                        <strong>Результат:</strong> ${this.correctAnswersCount} из ${this.currentQuizQuestions.length} правильно
                        ${this.correctAnswersCount === this.currentQuizQuestions.length ? 
                            '✅ Отлично! Тест пройден.' : 
                            '🔄 Попробуйте еще раз.'}
                    </div>
                    <button class="btn-secondary" id="quiz-retry">Пройти заново</button>
                    <button class="btn-primary" id="quiz-continue">Продолжить</button>
                `}
            </div>
        `;

        this.setupQuizEventListeners();
        
        if (!this.isQuizSubmitted) {
            this.setupOptionListeners();
        } else {
            this.highlightCorrectAnswers();
        }
    }

    setupOptionListeners() {
        document.querySelectorAll('.quiz-option input[type="radio"]').forEach(radio => {
            radio.addEventListener('change', (e) => {
                const questionId = e.target.closest('.quiz-question').dataset.questionId;
                const answer = parseInt(e.target.value);
                this.userAnswers[questionId] = answer;
                console.log(`Ответ на вопрос ${questionId}: ${answer}`);
            });
        });

        document.getElementById('quiz-retry')?.addEventListener('click', () => this.retryQuiz());
        document.getElementById('quiz-continue')?.addEventListener('click', () => this.continueAfterQuiz());
    }

    async submitQuiz() {
        try {
            if (!this.authManager.isAuthenticated()) {
                this.uiManager.showToast('Войдите в систему, чтобы отправить ответы', 'warning');
                return;
            }

            const answeredCount = Object.keys(this.userAnswers).length;
            const totalQuestions = this.currentQuizQuestions.length;

            if (answeredCount < totalQuestions) {
                const confirmSubmit = window.confirm(
                    `Вы ответили на ${answeredCount} из ${totalQuestions} вопросов. 
                    Отправить ответы? Неотвеченные вопросы будут засчитаны как неправильные.`
                );
                if (!confirmSubmit) return;
            }

            this.uiManager.showButtonLoading('quiz-submit', true);

            const answers = Object.entries(this.userAnswers).map(([questionId, userAnswer]) => ({
                questionId: questionId,
                userAnswer: userAnswer
            }));

            const lessonId = this.currentQuizQuestions[0]?.lessonId;
            if (!lessonId) {
                throw new Error('Не удалось определить идентификатор урока');
            }

            // Отправляем ответы
            const result = await this.api.submitQuizAnswers(lessonId, answers);
            
            if (result.success) {
                this.isQuizSubmitted = true;
                this.correctAnswersCount = result.result?.correctAnswers || 0;
                
                // Сразу отмечаем урок как завершенный
                if (this.correctAnswersCount === totalQuestions) {
                    await this.markLessonAsCompleted(lessonId);
                }
                
                this.renderQuiz(); // Перерисовываем с результатами
                this.uiManager.showToast('Ответы проверены!', 'success');
                
                // Автоматически обновляем прогресс
                this.updateProgressInUI();
            } else {
                throw new Error(result.error || 'Не удалось проверить ответы');
            }

        } catch (error) {
            console.error('Failed to submit quiz:', error);
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('quiz-submit', false);
        }
    }

    highlightCorrectAnswers() {
        document.querySelectorAll('.quiz-question').forEach(questionEl => {
            const questionId = questionEl.dataset.questionId;
            const question = this.currentQuizQuestions.find(q => q.id === questionId);
            if (!question) return;

            const options = questionEl.querySelectorAll('.quiz-option');
            options.forEach((optionEl, index) => {
                const optionNum = index + 1;
                
                // Отключаем все радиокнопки
                const radio = optionEl.querySelector('input[type="radio"]');
                if (radio) radio.disabled = true;
                
                // Подсвечиваем правильный ответ зеленым
                if (optionNum === question.correctOption) {
                    optionEl.classList.add('correct-answer');
                }
                
                // Подсвечиваем неправильный ответ красным
                const userAnswer = this.userAnswers[questionId];
                if (userAnswer === optionNum && userAnswer !== question.correctOption) {
                    optionEl.classList.add('wrong-answer');
                }
            });
        });
    }

    async markLessonAsCompleted(lessonId) {
        try {
            const result = await this.api.completeLesson(lessonId);
            if (result.success) {
                console.log('Lesson marked as completed:', lessonId);
                this.uiManager.showToast('Урок завершен!', 'success');
                
                // Обновляем UI сразу
                this.updateProgressInUI();
            }
        } catch (error) {
            console.error('Failed to mark lesson as completed:', error);
        }
    }

    updateProgressInUI() {
        // Обновляем кнопку в сайдбаре
        const currentLessonId = this.currentQuizQuestions[0]?.lessonId;
        if (currentLessonId) {
            const lessonElement = document.querySelector(`[data-lesson-id="${currentLessonId}"]`);
            if (lessonElement) {
                lessonElement.classList.add('completed');
                const infoDiv = lessonElement.querySelector('.lesson-info');
                if (infoDiv) {
                    const titleDiv = infoDiv.querySelector('.lesson-title');
                    if (titleDiv && !titleDiv.textContent.includes('✓')) {
                        titleDiv.textContent = '✓ ' + titleDiv.textContent;
                    }
                }
            }
        }
    }

    retryQuiz() {
        this.userAnswers = {};
        this.isQuizSubmitted = false;
        this.correctAnswersCount = 0;
        this.renderQuiz();
        this.uiManager.showToast('Тест начат заново', 'info');
    }

    continueAfterQuiz() {
        this.uiManager.hideQuizSection();
        if (this.correctAnswersCount === this.currentQuizQuestions.length) {
            this.uiManager.showToast('Тест успешно пройден!', 'success');
        }
    }

    closeQuiz() {
        this.uiManager.hideQuizSection();
    }
}