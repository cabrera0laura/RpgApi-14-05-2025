using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RpgApi.Data;
using RpgApi.Models;

namespace RpgApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DisputasController : ControllerBase
    {
        private readonly DataContext _context;

        public DisputasController(DataContext context)
        {
            _context = context;
        }

        [HttpPost("Arma")]
        public async Task<IActionResult> AtaqueComArmaAsync(Disputa d)
        {
            try
            {
                Personagem? atacante = await _context.TB_PERSONAGENS
                   .Include(p => p.Arma)
                   .FirstOrDefaultAsync(p => p.Id == d.AtacanteId);

                Personagem? oponente = await _context.TB_PERSONAGENS
                    .FirstOrDefaultAsync(p => p.Id == d.OponenteId);

                int dano = atacante.Arma.Dano + new Random().Next(atacante.Forca);
                dano = dano - new Random().Next(oponente.Defesa);

                if (dano > 0)                
                    oponente.PontosVida = oponente.PontosVida - dano;                
                if (oponente.PontosVida <= 0)                
                    d.Narracao = $"{oponente.Nome} foi derrotado!";                

                _context.TB_PERSONAGENS.Update(oponente);
                await _context.SaveChangesAsync();

                StringBuilder dados = new StringBuilder();
                dados.AppendFormat(" Atacante: {0}. ", atacante.Nome);
                dados.AppendFormat(" Oponente: {0}. ", oponente.Nome);
                dados.AppendFormat(" Pontos de vida do atacante: {0}. ", atacante.PontosVida);
                dados.AppendFormat(" Pontos de vida do oponente: {0}. ", oponente.PontosVida);
                dados.AppendFormat(" Arma Utilizada: {0}. ", atacante.Arma.Nome);
                dados.AppendFormat(" Dano: {0}. ", dano);

                d.Narracao += dados.ToString();
                d.DataDisputa = DateTime.Now;
                _context.TB_DISPUTAS.Add(d);
                _context.SaveChanges();

                return Ok(d);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message + " - " + ex.InnerException);
            }
        }

        [HttpPost("Habilidade")]
        public async Task<IActionResult> AtaqueComHabilidadeAsync(Disputa d)
        {
            try
            {
                Personagem atacante = await _context.TB_PERSONAGENS
                    .Include(p => p.PersonagemHabilidades).ThenInclude(ph => ph.Habilidade)
                    .FirstOrDefaultAsync(p => p.Id == d.AtacanteId);

                Personagem oponente = await _context.TB_PERSONAGENS
                    .FirstOrDefaultAsync(p => p.Id == d.OponenteId);

                PersonagemHabilidade ph = await _context.TB_PERSONAGENS_HABILIDADES
                    .Include(p => p.Habilidade).FirstOrDefaultAsync(phBusca => phBusca.HabilidadeId == d.HabilidadeId
                     && phBusca.PersonagemId == d.AtacanteId);


                     //Verificar se essa linha acima não vai gerar falha                     

                if (ph == null)
                    d.Narracao = $"{atacante.Nome} não possui esta habilidade";
                else
                {
                    int dano = ph.Habilidade.Dano + (new Random().Next(atacante.Inteligencia));
                    dano = dano - new Random().Next(oponente.Defesa);

                    if (dano > 0)                    
                        oponente.PontosVida = oponente.PontosVida - dano;                                            
                    if (oponente.PontosVida <= 0)                    
                        d.Narracao += $"{oponente.Nome} foi derrotado!";                    

                    _context.TB_PERSONAGENS.Update(oponente);
                    await _context.SaveChangesAsync();

                    StringBuilder dados = new StringBuilder();
                    dados.AppendFormat(" Atacante: {0}. ", atacante.Nome);
                    dados.AppendFormat(" Oponente: {0}. ", oponente.Nome);
                    dados.AppendFormat(" Pontos de vida do atacante: {0}. ", atacante.PontosVida);
                    dados.AppendFormat(" Pontos de vida do oponente: {0}. ", oponente.PontosVida);
                    dados.AppendFormat(" Habilidade Utilizada: {0}. ", ph.Habilidade.Nome);
                    dados.AppendFormat(" Dano: {0}. ", dano);

                    d.Narracao += dados.ToString();
                    d.DataDisputa = DateTime.Now;
                    _context.TB_DISPUTAS.Add(d);
                    _context.SaveChanges();
                }
                return Ok(d);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message + " - " + ex.InnerException);
            }
        }    

         [HttpPost("DisputaEmGrupo")]
         public async Task<IActionResult>DisputaEmGrupoAsync(Disputa d)
         {
            try
            {
                d.Resultados = new List<string>(); //Instancia a lista de resultados

                //Busca na base de dados dos personagens informados no parametro incluindo Armas e habilidades
                List<Personagem> personagens = await _context.Personagens
                .Include(p => p.Arma)
                .Include(p => p.PersonagemHabilidades).ThenInclude(ph => ph.Habilidade)
                .Where(p => d.ListIdPersonagens.Contains(p.Id)).ToListAsync();

                int qtdPersonagensVivos = personagens.FinAll(p => p.PontosVida > 0).Count;

                while (qtdPersonagensVivos > 1)
                {
                    // Selecione personagens com pontos de vida positivo e depois faz sorteio.
                    List<Personagem> atacante = personagens.Where(p => p.PontosVida > 0 ).ToList();
                    Personagem atacante = atacante[new Random().Next(atacantes.Count)];
                    d.AtacanteId = atacante.Id;

                    //Seleciona personagens com pontos de vida positivos, exceto o atacante escolhido e depois faz sorteio
                    List<Personagem> oponentes = personagens.Where(p => p.Id != atacante.Id && p.PontosVida > 0).ToList();
                    Personagem oponente = oponentes[new Random().Next(oponentes.Count)];
                    d.OponenteId = oponente.Id;

                    //declara e redefine a cada passagem do while o valor das variaveis que serão usadas.
                    int dano = 0;
                    string ataqueUsado = string.Empty;
                    string resultado = string.Empty;

                    //Sorteio entre 0 e 1: 0  é um ataque com arma e 1 é um ataque com habilidades.
                    bool ataqueUsaArma = (new Random().Next(1) == 0);

                    if(ataqueUsaArma && atacante.Arma != null)
                    {
                        // Programação do ataque com arma caso o atacante possua arma (o !=null) do if

                        //Sorteio da Força
                        dano = atacante.Arma.Dano + (new Random().Next(atacante.Forca));
                        dano = dano - new Random().Next(oponente.Defesa); //Sorteio da defesa.
                        ataqueUsado = atacante.Arma.Nome;

                        if(dano > 0)
                        oponente.PontosVida = oponente.PontosVida -(int)dano;

                        //Formata a mensagem
                        resultado =
                            string.Format{"{0} atacou {1} usando {2} com o dano {3} ", atacante.Nome, oponente.Nome,ataqueUsado, dano};
                        d.Narracao += resultado; //Concatena o resultado com as narrações existentes.
                        d.Resultados.Add(resultado); //Adicionar o resultado atual na lista de resultados.

                    }
                    else if (atacante.PersonagemHabilidades.Count != 0) //Verifica se o personagem tem Habilidades
                    {
                        //Programação do ataque com habilidade.

                        //Realiza o sorteio entre as habilidades ************************************************************************************************************************
                    }
                }


                _context.Personagens.UpdateRange(personagens);
                await _context.SaveChangesAsync();

                return ok(d); //retorna os dados de disputas
            }
            catch(System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
         }   


    }
}