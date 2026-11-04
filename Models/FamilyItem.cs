using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.Models
{
    /// <summary>
    /// Representa uma família encontrada no arquivo de origem.
    /// Implementa INotifyPropertyChanged para suportar binding WPF na interface.
    /// </summary>
    public class FamilyItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// Nome da família (ex: "Porta de Correr 2 Folhas").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Indica se o usuário selecionou esta família para importação.
        /// Notifica a UI quando alterado via binding.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Referência ao objeto Family do Revit API.
        /// Usado internamente para a operação de cópia.
        /// </summary>
        public Family? RevitFamily { get; set; }

        /// <summary>
        /// Nome da categoria da família (ex: "Portas", "Janelas", "Mobiliário").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// ElementId da família no documento de origem.
        /// Necessário para copiar elementos entre documentos.
        /// </summary>
        public ElementId? FamilyId { get; set; }

        /// <summary>
        /// Quantidade de tipos (FamilySymbol) disponíveis nesta família.
        /// Exibido na interface para referência do usuário.
        /// </summary>
        public int TypeCount { get; set; }

        // ── INotifyPropertyChanged ──────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Dispara o evento PropertyChanged para atualizar o binding WPF.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Representação textual para debug.
        /// </summary>
        public override string ToString()
        {
            return $"{Name} [{Category}] ({TypeCount} tipos)";
        }
    }
}
