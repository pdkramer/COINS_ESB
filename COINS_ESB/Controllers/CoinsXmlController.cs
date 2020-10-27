using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using COINS_ESB.Models;
using System.IO;
using System.Text;
using System;

namespace COINS_ESB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoinsXmlController : ControllerBase
    {
        private readonly ApexCOINContext _context;

        public CoinsXmlController(ApexCOINContext context)
        {
            _context = context;
        }

        // GET: api/CoinsXml
        [HttpGet]
        public IEnumerable<CoinsXml> GetCoinsXml()
        {
            //return _context.CoinsXml.Select(s => s.Id).ToList();
            return _context.CoinsXml.Take(Support.MaxBatchSize);
        }

        // GET: api/CoinsXml/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCoinsXml([FromRoute] long id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var coinsXml = await _context.CoinsXml.FindAsync(id);

            if (coinsXml == null)
            {
                return NotFound();
            }

            return Ok(coinsXml);
        }

        // POST: api/CoinsXml
        [HttpPost]
        // doesn't work... [Produces("application/xml")]
        public async Task<ContentResult> PostCoinsXml()
        {
            string BodyXML;

            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                BodyXML = await reader.ReadToEndAsync();
            }

            CoinsXml coinsXml = new CoinsXml { RawXml = BodyXML };

            _context.CoinsXml.Add(coinsXml);
            await _context.SaveChangesAsync();

            return Content($"<?xml version=\"1.0\" encoding=\"utf-8\"?><root><id>{coinsXml.Id}</id></root>");
                //CreatedAtAction("GetCoinsXml", new { id = coinsXml.Id });
        }

        // DELETE: api/CoinsXml/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCoinsXml([FromRoute] long id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var coinsXml = await _context.CoinsXml.FindAsync(id);
            if (coinsXml == null)
            {
                return NotFound();
            }

            _context.CoinsXml.Remove(coinsXml);

            CoinsArchiveXml archive = new CoinsArchiveXml
            {
                Id = coinsXml.Id,
                RecDate = coinsXml.RecDate,
                ArchiveDate = DateTime.Now,
                RawXml = coinsXml.RawXml
            };

            await _context.AddAsync<CoinsArchiveXml>(archive);

            await _context.SaveChangesAsync(); //this implicitly does the work within a database transaction

            return Ok(coinsXml);
        }

        private bool CoinsXmlExists(long id)
        {
            return _context.CoinsXml.Any(e => e.Id == id);
        }
    }
}