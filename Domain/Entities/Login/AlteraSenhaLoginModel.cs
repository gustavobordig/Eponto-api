using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Login
{
    public class AlteraSenhaLoginModel
    {
        public string senha { get; set; }
        public string email { get; set; }
        /// <summary>Código recebido por e-mail em RecuperarSenha</summary>
        public int codigo { get; set; }
    }
}
