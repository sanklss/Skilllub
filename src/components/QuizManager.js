import { ApiService } from '../services/ApiService.js';

export class QuizManager {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentQuizQuestions = [];
        this.currentQuestionIndex = 0;
        this.userAnswers = {};
    }

    initialize() {
        this.setupQuizEventListeners();
    }

    setupQuizEventListeners() {
        document.getElementById('quiz-next')?.addEventListener('click', () => this.goToNextQuestion());
        document.getElementById('quiz-prev')?.addEventListener('click', () => this.goToPrevQuestion());
        document.getElementById('quiz-close')?.addEventListener('click', () => this.closeQuiz());
    }

    async loadQuizQuestions(lessonId) {
        try {
            console.log('Loading quiz questions for lesson:', lessonId);
            
            const result = await this.api.getQuizQuestions(lessonId);
            
            if (result.success && result.questions && result.questions.length > 0) {
                console.log('Quiz questions loaded:::', result.questions.length);
                
                this.currentQuizQuestions = result.questions;
                this.currentQuestionIndex = 0;
                this.userAnswers = {};
                
                this.uiManager.showQuizSection();
                this.renderQuizQuestion();
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

    renderQuizQuestion() {
        if (this.currentQuizQuestions.length === 0) return;
        
        const question = this.currentQuizQuestions[this.currentQuestionIndex];
        
        const quizContainer = document.getElementById('quiz-section');
        if (!quizContainer.querySelector('.quiz-container')) {
            const template = document.getElementById('quiz-container-template');
            quizContainer.innerHTML = template.innerHTML;
        }
        
        const quizQuestion = document.getElementById('quiz-question');
        const quizOptions = document.getElementById('quiz-options');
        const quizProgress = document.getElementById('quiz-progress');
        const quizPrev = document.getElementById('quiz-prev');
        const quizNext = document.getElementById('quiz-next');
        const quizClose = document.getElementById('quiz-close');
        
        if (quizProgress) {
            quizProgress.textContent = `Вопрос ${this.currentQuestionIndex + 1} из ${this.currentQuizQuestions.length}`;
        }
        
        if (quizQuestion) {
            quizQuestion.textContent = question.questionText;
        }
        
        if (quizOptions) {
            this.renderQuizOptions(quizOptions, question);
        }
        
        if (quizPrev) {
            quizPrev.disabled = this.currentQuestionIndex === 0;
        }
        
        if (quizNext) {
            quizNext.disabled = this.currentQuestionIndex === this.currentQuizQuestions.length - 1;
        }
        
        const userAnswer = this.userAnswers[question.id];
        if (userAnswer && quizOptions) {
            const selectedOption = quizOptions.querySelector(`[data-option="${userAnswer}"]`);
            if (selectedOption) {
                selectedOption.classList.add('selected');
            }
        }
    }

    renderQuizOptions(container, question) {
        container.innerHTML = '';
        
        const options = [
            { number: 1, letter: 'A', text: question.option1 },
            { number: 2, letter: 'B', text: question.option2 },
            { number: 3, letter: 'C', text: question.option3 },
            { number: 4, letter: 'D', text: question.option4 }
        ];
        
        options.forEach(option => {
            const optionElement = document.createElement('div');
            optionElement.className = 'quiz-option';
            optionElement.setAttribute('data-option', option.number);
            optionElement.innerHTML = `<strong>${option.letter})</strong> ${option.text}`;
            
            optionElement.addEventListener('click', (e) => this.handleOptionSelect(e));
            container.appendChild(optionElement);
        });
    }

    handleOptionSelect(event) {
        const optionElement = event.currentTarget;
        const question = this.currentQuizQuestions[this.currentQuestionIndex];
        const selectedOption = parseInt(optionElement.getAttribute('data-option'));
        
        optionElement.parentElement.querySelectorAll('.quiz-option').forEach(opt => {
            opt.classList.remove('selected');
        });
        
        optionElement.classList.add('selected');
        this.userAnswers[question.id] = selectedOption;
    }

    goToNextQuestion() {
        if (this.currentQuestionIndex < this.currentQuizQuestions.length - 1) {
            this.currentQuestionIndex++;
            this.renderQuizQuestion();
        }
    }

    goToPrevQuestion() {
        if (this.currentQuestionIndex > 0) {
            this.currentQuestionIndex--;
            this.renderQuizQuestion();
        }
    }

    closeQuiz() {
        this.uiManager.hideQuizSection();
    }
}