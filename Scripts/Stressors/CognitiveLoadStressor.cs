/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements cognitive load through additional mental tasks for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Creates cognitive load through math problems, memory tasks, and pattern recognition
    /// </summary>
    public class CognitiveLoadStressor : MonoBehaviour, IStressor {
        [Header("Task Settings")]
        [SerializeField] private float _taskInterval = 10f;
        [SerializeField] private float _taskDuration = 5f;
        [SerializeField] private bool _requireResponse = true;
        [SerializeField] private float _responseTimeout = 3f;
        
        [Header("Task Types")]
        [SerializeField] private bool _enableMathTasks = true;
        [SerializeField] private bool _enableMemoryTasks = true;
        [SerializeField] private bool _enablePatternTasks = true;
        [SerializeField] private bool _enableCountingTasks = true;
        [SerializeField] private bool _enableSpatialTasks = true;
        
        [Header("Difficulty")]
        [SerializeField] private DifficultyLevel _baseDifficulty = DifficultyLevel.Medium;
        [SerializeField] private bool _adaptiveDifficulty = true;
        [SerializeField] private float _correctAnswerBonus = 0.1f;
        [SerializeField] private float _wrongAnswerPenalty = 0.15f;
        
        [Header("UI Settings")]
        [SerializeField] private Vector3 _taskUIOffset = new Vector3(0, 1f, 2f);
        [SerializeField] private float _taskUISize = 0.5f;
        [SerializeField] private Color _taskColor = Color.cyan;
        [SerializeField] private Font _taskFont;
        
        [Header("Feedback")]
        [SerializeField] private bool _provideFeedback = true;
        [SerializeField] private AudioClip _correctSound;
        [SerializeField] private AudioClip _incorrectSound;
        [SerializeField] private AudioClip _taskAppearSound;
        
        [Header("Simultaneous Tasks")]
        [SerializeField] private bool _allowMultipleTasks = true;
        [SerializeField] private int _maxSimultaneousTasks = 3;
        
        // Task templates
        private readonly string[] _mathTemplates = {
            "{0} + {1} = ?",
            "{0} - {1} = ?",
            "{0} × {1} = ?",
            "{0} ÷ {1} = ?",
            "({0} + {1}) × {2} = ?",
            "{0}² + {1} = ?"
        };
        
        private readonly string[] _memorySequences = {
            "Remember: {0}, {1}, {2}",
            "Sequence: {0} → {1} → {2} → {3}",
            "Code: {0}-{1}-{2}-{3}"
        };
        
        private readonly string[] _patternTasks = {
            "Complete: {0}, {1}, {2}, ?",
            "Pattern: {0}, skip {1}, {2}, skip ?, {3}",
            "Next in sequence: {0}, {1}, {2}, ?"
        };
        
        private readonly string[] _countingTasks = {
            "Count backwards from {0} by {1}",
            "Count red objects",
            "Count hostile avatars",
            "Count even numbers shown"
        };
        
        // Interface implementation
        private string _name = "CognitiveLoad";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private Canvas _taskCanvas;
        private List<ActiveTask> _activeTasks;
        private Dictionary<int, object> _taskAnswers;
        private Coroutine _taskCoroutine;
        private float _performanceScore = 0.5f;
        private Queue<bool> _recentPerformance;
        private AudioSource _audioSource;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private enum TaskType {
            Math, Memory, Pattern, Counting, Spatial
        }
        
        private enum DifficultyLevel {
            Easy, Medium, Hard, Expert
        }
        
        private class ActiveTask {
            public int id;
            public TaskType type;
            public string question;
            public object answer;
            public GameObject uiObject;
            public float startTime;
            public bool answered;
        }
        
        private void Awake() {
            _activeTasks = new List<ActiveTask>();
            _taskAnswers = new Dictionary<int, object>();
            _recentPerformance = new Queue<bool>(10);
            
            SetupUI();
            SetupAudio();
            GenerateSounds();
        }
        
        /// <summary>
        /// Sets up the task UI canvas
        /// </summary>
        private void SetupUI() {
            GameObject canvasObject = new GameObject("CognitiveTaskCanvas");
            canvasObject.transform.SetParent(transform);
            _taskCanvas = canvasObject.AddComponent<Canvas>();
            _taskCanvas.renderMode = RenderMode.WorldSpace;
            _taskCanvas.worldCamera = Camera.main;
            
            // Set canvas size
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(5f, 3f) * _taskUISize;
            
            // Add canvas scaler
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            
            // Start hidden
            canvasObject.SetActive(false);
            
            // Default font
            if (_taskFont == null) {
                _taskFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }
        
        /// <summary>
        /// Sets up audio components
        /// </summary>
        private void SetupAudio() {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.5f;
        }
        
        /// <summary>
        /// Generates sound effects
        /// </summary>
        private void GenerateSounds() {
            if (_correctSound == null) {
                _correctSound = GenerateCorrectSound();
            }
            
            if (_incorrectSound == null) {
                _incorrectSound = GenerateIncorrectSound();
            }
            
            if (_taskAppearSound == null) {
                _taskAppearSound = GenerateTaskAppearSound();
            }
        }
        
        /// <summary>
        /// Generates correct answer sound
        /// </summary>
        private AudioClip GenerateCorrectSound() {
            int sampleRate = 44100;
            float duration = 0.3f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Ascending tone
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                float frequency = Mathf.Lerp(500f, 800f, t);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Exp(-t * 2f) * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create("CorrectSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates incorrect answer sound
        /// </summary>
        private AudioClip GenerateIncorrectSound() {
            int sampleRate = 44100;
            float duration = 0.3f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Descending tone
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                float frequency = Mathf.Lerp(400f, 200f, t);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Exp(-t * 2f) * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create("IncorrectSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates task appear sound
        /// </summary>
        private AudioClip GenerateTaskAppearSound() {
            int sampleRate = 44100;
            float duration = 0.2f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Bell-like sound
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 1000f * t) * Mathf.Exp(-t * 5f) * 0.2f;
                samples[i] += Mathf.Sin(2 * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 5f) * 0.1f;
            }
            
            AudioClip clip = AudioClip.Create("TaskAppearSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Initializes the cognitive load stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the cognitive load stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            _taskCanvas.gameObject.SetActive(true);
            
            if (_taskCoroutine != null) {
                StopCoroutine(_taskCoroutine);
            }
            _taskCoroutine = StartCoroutine(TaskGenerationCoroutine());
        }
        
        /// <summary>
        /// Deactivates the cognitive load stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            if (_taskCoroutine != null) {
                StopCoroutine(_taskCoroutine);
                _taskCoroutine = null;
            }
            
            // Clear all active tasks
            ClearAllTasks();
            
            _taskCanvas.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Main task generation coroutine
        /// </summary>
        private IEnumerator TaskGenerationCoroutine() {
            while (_isActive) {
                // Wait for next task
                float waitTime = _taskInterval * (2f - _intensity);
                yield return new WaitForSeconds(waitTime);
                
                if (!_isActive) break;
                
                // Check task limit
                if (!_allowMultipleTasks && _activeTasks.Count > 0) {
                    continue;
                }
                
                if (_activeTasks.Count >= _maxSimultaneousTasks) {
                    // Remove oldest task
                    RemoveTask(_activeTasks[0]);
                }
                
                // Generate new task
                TaskType taskType = SelectTaskType();
                ActiveTask task = GenerateTask(taskType);
                
                if (task != null) {
                    _activeTasks.Add(task);
                    StartCoroutine(TaskLifecycle(task));
                }
            }
        }
        
        /// <summary>
        /// Selects a task type based on enabled options
        /// </summary>
        private TaskType SelectTaskType() {
            List<TaskType> availableTypes = new List<TaskType>();
            
            if (_enableMathTasks) availableTypes.Add(TaskType.Math);
            if (_enableMemoryTasks) availableTypes.Add(TaskType.Memory);
            if (_enablePatternTasks) availableTypes.Add(TaskType.Pattern);
            if (_enableCountingTasks) availableTypes.Add(TaskType.Counting);
            if (_enableSpatialTasks) availableTypes.Add(TaskType.Spatial);
            
            if (availableTypes.Count == 0) {
                availableTypes.Add(TaskType.Math); // Default
            }
            
            return availableTypes[Random.Range(0, availableTypes.Count)];
        }
        
        /// <summary>
        /// Generates a specific task
        /// </summary>
        private ActiveTask GenerateTask(TaskType type) {
            ActiveTask task = new ActiveTask {
                id = Random.Range(1000, 9999),
                type = type,
                startTime = Time.time,
                answered = false
            };
            
            DifficultyLevel difficulty = GetCurrentDifficulty();
            
            switch (type) {
                case TaskType.Math:
                    GenerateMathTask(task, difficulty);
                    break;
                case TaskType.Memory:
                    GenerateMemoryTask(task, difficulty);
                    break;
                case TaskType.Pattern:
                    GeneratePatternTask(task, difficulty);
                    break;
                case TaskType.Counting:
                    GenerateCountingTask(task, difficulty);
                    break;
                case TaskType.Spatial:
                    GenerateSpatialTask(task, difficulty);
                    break;
            }
            
            // Create UI for task
            CreateTaskUI(task);
            
            // Play appear sound
            if (_taskAppearSound != null && _audioSource != null) {
                _audioSource.PlayOneShot(_taskAppearSound, 0.5f);
            }
            
            return task;
        }
        
        /// <summary>
        /// Generates a math task
        /// </summary>
        private void GenerateMathTask(ActiveTask task, DifficultyLevel difficulty) {
            int a, b, c;
            int answer;
            string template;
            
            switch (difficulty) {
                case DifficultyLevel.Easy:
                    a = Random.Range(1, 10);
                    b = Random.Range(1, 10);
                    answer = a + b;
                    template = _mathTemplates[0]; // Addition
                    task.question = string.Format(template, a, b);
                    break;
                    
                case DifficultyLevel.Medium:
                    a = Random.Range(10, 50);
                    b = Random.Range(10, 50);
                    if (Random.value > 0.5f) {
                        answer = a + b;
                        template = _mathTemplates[0];
                    } else {
                        answer = a - b;
                        template = _mathTemplates[1];
                    }
                    task.question = string.Format(template, a, b);
                    break;
                    
                case DifficultyLevel.Hard:
                    a = Random.Range(5, 15);
                    b = Random.Range(5, 15);
                    answer = a * b;
                    template = _mathTemplates[2]; // Multiplication
                    task.question = string.Format(template, a, b);
                    break;
                    
                case DifficultyLevel.Expert:
                    a = Random.Range(5, 10);
                    b = Random.Range(5, 10);
                    c = Random.Range(2, 5);
                    answer = (a + b) * c;
                    template = _mathTemplates[4]; // Complex
                    task.question = string.Format(template, a, b, c);
                    break;
                    
                default:
                    a = Random.Range(1, 20);
                    b = Random.Range(1, 20);
                    answer = a + b;
                    template = _mathTemplates[0];
                    task.question = string.Format(template, a, b);
                    break;
            }
            
            task.answer = answer;
            
            // Add multiple choice options
            task.question += $"\nA: {answer} B: {answer + Random.Range(1, 5)} C: {answer - Random.Range(1, 5)}";
        }
        
        /// <summary>
        /// Generates a memory task
        /// </summary>
        private void GenerateMemoryTask(ActiveTask task, DifficultyLevel difficulty) {
            int sequenceLength = difficulty switch {
                DifficultyLevel.Easy => 3,
                DifficultyLevel.Medium => 4,
                DifficultyLevel.Hard => 5,
                DifficultyLevel.Expert => 6,
                _ => 4
            };
            
            List<int> sequence = new List<int>();
            for (int i = 0; i < sequenceLength; i++) {
                sequence.Add(Random.Range(1, 10));
            }
            
            task.question = "Remember: " + string.Join(", ", sequence);
            task.answer = sequence;
            
            // Will ask for recall later
            StartCoroutine(DelayedMemoryRecall(task, 3f));
        }
        
        /// <summary>
        /// Generates a pattern task
        /// </summary>
        private void GeneratePatternTask(ActiveTask task, DifficultyLevel difficulty) {
            int start = Random.Range(1, 10);
            int step = Random.Range(2, 6);
            
            List<int> pattern = new List<int>();
            for (int i = 0; i < 3; i++) {
                pattern.Add(start + i * step);
            }
            
            int answer = start + 3 * step;
            
            task.question = $"Pattern: {string.Join(", ", pattern)}, ?";
            task.answer = answer;
            
            // Add options
            task.question += $"\nA: {answer} B: {answer + step} C: {answer - step}";
        }
        
        /// <summary>
        /// Generates a counting task
        /// </summary>
        private void GenerateCountingTask(ActiveTask task, DifficultyLevel difficulty) {
            int start = Random.Range(50, 100);
            int step = difficulty switch {
                DifficultyLevel.Easy => 1,
                DifficultyLevel.Medium => 3,
                DifficultyLevel.Hard => 7,
                DifficultyLevel.Expert => 13,
                _ => 3
            };
            
            task.question = $"Count backwards from {start} by {step}\nWhat's the 5th number?";
            task.answer = start - (4 * step);
            
            int correctAnswer = (int)task.answer;
            task.question += $"\nA: {correctAnswer} B: {correctAnswer - step} C: {correctAnswer + step}";
        }
        
        /// <summary>
        /// Generates a spatial task
        /// </summary>
        private void GenerateSpatialTask(ActiveTask task, DifficultyLevel difficulty) {
            string[] directions = { "North", "South", "East", "West" };
            string[] turns = { "left", "right", "around" };
            
            string startDir = directions[Random.Range(0, 4)];
            string turn = turns[Random.Range(0, 3)];
            
            string answer = GetDirectionAfterTurn(startDir, turn);
            
            task.question = $"Facing {startDir}, turn {turn}.\nWhich direction?";
            task.answer = answer;
            
            // Add options
            task.question += $"\nA: {answer} B: {directions[Random.Range(0, 4)]} C: {directions[Random.Range(0, 4)]}";
        }
        
        /// <summary>
        /// Gets direction after turn
        /// </summary>
        private string GetDirectionAfterTurn(string start, string turn) {
            string[] directions = { "North", "East", "South", "West" };
            int index = System.Array.IndexOf(directions, start);
            
            switch (turn) {
                case "left":
                    index = (index - 1 + 4) % 4;
                    break;
                case "right":
                    index = (index + 1) % 4;
                    break;
                case "around":
                    index = (index + 2) % 4;
                    break;
            }
            
            return directions[index];
        }
        
        /// <summary>
        /// Creates UI for a task
        /// </summary>
        private void CreateTaskUI(ActiveTask task) {
            GameObject taskPanel = new GameObject($"Task_{task.id}");
            taskPanel.transform.SetParent(_taskCanvas.transform);
            
            // Add background
            Image background = taskPanel.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.8f);
            
            RectTransform rect = taskPanel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 200);
            
            // Position based on number of active tasks
            float yOffset = _activeTasks.Count * 220;
            rect.anchoredPosition = new Vector2(0, yOffset);
            
            // Add text
            GameObject textObject = new GameObject("TaskText");
            textObject.transform.SetParent(taskPanel.transform);
            
            Text taskText = textObject.AddComponent<Text>();
            taskText.text = task.question;
            taskText.font = _taskFont;
            taskText.fontSize = 24;
            taskText.color = _taskColor;
            taskText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            task.uiObject = taskPanel;
            
            // Position in world space
            if (Camera.main != null) {
                Vector3 worldPos = Camera.main.transform.position + Camera.main.transform.forward * 3f + _taskUIOffset;
                taskPanel.transform.position = worldPos;
                taskPanel.transform.LookAt(Camera.main.transform);
                taskPanel.transform.Rotate(0, 180, 0);
            }
        }
        
        /// <summary>
        /// Task lifecycle management
        /// </summary>
        private IEnumerator TaskLifecycle(ActiveTask task) {
            float elapsed = 0f;
            
            while (elapsed < _taskDuration && !task.answered) {
                elapsed += Time.deltaTime;
                
                // Flash when time is running out
                if (elapsed > _taskDuration * 0.7f && task.uiObject != null) {
                    Image bg = task.uiObject.GetComponent<Image>();
                    if (bg != null) {
                        float flash = Mathf.Sin(elapsed * 10f);
                        bg.color = new Color(flash * 0.5f, 0, 0, 0.8f);
                    }
                }
                
                yield return null;
            }
            
            // Check if answered
            if (!task.answered && _requireResponse) {
                // Task failed (no response)
                UpdatePerformance(false);
                ShowFeedback(task, false, "TIME OUT");
            }
            
            // Remove task after delay
            yield return new WaitForSeconds(1f);
            RemoveTask(task);
        }
        
        /// <summary>
        /// Delayed memory recall
        /// </summary>
        private IEnumerator DelayedMemoryRecall(ActiveTask task, float delay) {
            yield return new WaitForSeconds(delay);
            
            if (task.uiObject != null) {
                Text text = task.uiObject.GetComponentInChildren<Text>();
                if (text != null) {
                    text.text = "What was the sequence?\nA: Check your memory!";
                }
            }
        }
        
        /// <summary>
        /// Removes a task
        /// </summary>
        private void RemoveTask(ActiveTask task) {
            _activeTasks.Remove(task);
            _taskAnswers.Remove(task.id);
            
            if (task.uiObject != null) {
                Destroy(task.uiObject);
            }
        }
        
        /// <summary>
        /// Clears all active tasks
        /// </summary>
        private void ClearAllTasks() {
            foreach (var task in _activeTasks) {
                if (task.uiObject != null) {
                    Destroy(task.uiObject);
                }
            }
            _activeTasks.Clear();
            _taskAnswers.Clear();
        }
        
        /// <summary>
        /// Handles task answer input
        /// </summary>
        public void AnswerTask(int taskId, object answer) {
            ActiveTask task = _activeTasks.Find(t => t.id == taskId);
            if (task == null || task.answered) return;
            
            task.answered = true;
            bool correct = CheckAnswer(task, answer);
            
            UpdatePerformance(correct);
            ShowFeedback(task, correct);
            
            // Play feedback sound
            if (_audioSource != null) {
                _audioSource.PlayOneShot(correct ? _correctSound : _incorrectSound, 0.5f);
            }
        }
        
        /// <summary>
        /// Checks if answer is correct
        /// </summary>
        private bool CheckAnswer(ActiveTask task, object answer) {
            if (task.answer == null || answer == null) return false;
            
            if (task.answer is List<int> sequence && answer is List<int> givenSequence) {
                return sequence.SequenceEqual(givenSequence);
            }
            
            return task.answer.ToString() == answer.ToString();
        }
        
        /// <summary>
        /// Shows feedback for answer
        /// </summary>
        private void ShowFeedback(ActiveTask task, bool correct, string message = null) {
            if (!_provideFeedback || task.uiObject == null) return;
            
            Text text = task.uiObject.GetComponentInChildren<Text>();
            if (text != null) {
                text.text = message ?? (correct ? "CORRECT!" : $"WRONG! Answer: {task.answer}");
                text.color = correct ? Color.green : Color.red;
            }
        }
        
        /// <summary>
        /// Updates performance tracking
        /// </summary>
        private void UpdatePerformance(bool correct) {
            _recentPerformance.Enqueue(correct);
            if (_recentPerformance.Count > 10) {
                _recentPerformance.Dequeue();
            }
            
            _performanceScore = _recentPerformance.Count(p => p) / (float)_recentPerformance.Count;
            
            // Adjust difficulty if adaptive
            if (_adaptiveDifficulty) {
                if (correct) {
                    _intensity = Mathf.Min(1f, _intensity + _correctAnswerBonus);
                } else {
                    _intensity = Mathf.Max(0f, _intensity - _wrongAnswerPenalty);
                }
            }
        }
        
        /// <summary>
        /// Gets current difficulty based on performance
        /// </summary>
        private DifficultyLevel GetCurrentDifficulty() {
            if (_adaptiveDifficulty) {
                if (_performanceScore > 0.8f) return DifficultyLevel.Expert;
                if (_performanceScore > 0.6f) return DifficultyLevel.Hard;
                if (_performanceScore > 0.4f) return DifficultyLevel.Medium;
                return DifficultyLevel.Easy;
            }
            
            return _baseDifficulty;
        }
        
        /// <summary>
        /// Updates the stressor
        /// </summary>
        public void UpdateStressor() {
            // Update task positions to follow player
            if (Camera.main != null) {
                foreach (var task in _activeTasks) {
                    if (task.uiObject != null) {
                        Vector3 targetPos = Camera.main.transform.position + 
                                          Camera.main.transform.forward * 3f + _taskUIOffset;
                        task.uiObject.transform.position = Vector3.Lerp(
                            task.uiObject.transform.position,
                            targetPos,
                            Time.deltaTime * 2f
                        );
                        task.uiObject.transform.LookAt(Camera.main.transform);
                        task.uiObject.transform.Rotate(0, 180, 0);
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets current parameters
        /// </summary>
        public StressorParameters GetParameters() {
            return _parameters;
        }
        
        /// <summary>
        /// Updates parameters at runtime
        /// </summary>
        public void UpdateParameters(StressorParameters parameters) {
            _parameters = parameters;
            _intensity = _parameters.intensity;
        }
        
        private void OnDestroy() {
            if (_isActive) {
                Deactivate();
            }
        }
    }
}