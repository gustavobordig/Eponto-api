using Domain.Entities.Calendario;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs
{
    public class CalendarioDTO
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; }
        public List<CalendarioModel> Dias { get; set; }
    }
}
