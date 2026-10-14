using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Ponto
{
    public class ValidarSolicitacao
    {
        public bool Aprovado { get; set; }
        /// <summary>Explicação opcional para o funcionário</summary>
        public string? Comentario { get; set; }
    }
}
