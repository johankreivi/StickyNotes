namespace Notes.Api.Controllers;

using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notes.Api.AccessControl;
using Notes.Api.Database;
using Notes.Api.Models;
using Serilog.Core;

[Authorize]
[ApiController]
[Route("[controller]")]
public class NotesController : ControllerBase
{
    private readonly NotesDb _database;
    private readonly ILogger<NotesController> _logger;

    public NotesController(NotesDb database, ILogger<NotesController> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of all sticky notes written by a given author.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Note[] Get([FromQuery] string containing)
    {
        var authorizationHeader = Request.Headers["Authorization"];
        var user = BasicAuthenticationHandler.GetUserFrom(authorizationHeader);

        return _database.Notes
            .FromSqlRaw($"SELECT * FROM Notes WHERE Author='{user.Username}' AND Content LIKE '%{containing}%' ORDER BY Id")
            .ToArray();
    }

    /// <summary>
    /// Creates a new sticky note.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Note> Post([FromBody] CreateNote createNote)
    {
        var authorizationHeader = Request.Headers["Authorization"];
        var user = BasicAuthenticationHandler.GetUserFrom(authorizationHeader);

        var note = new Note
        {
            Author = user.Username,
            Content = createNote.Content,
        };

        _database.Add(note);
        _database.SaveChanges();

        return CreatedAtRoute("GetNoteById", new { noteId = note.Id }, note);
    }

    /// <summary>
    /// Retrieves a single sticky note.
    /// </summary>
    [HttpGet("{noteId}", Name = "GetNoteById")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Note> Get([FromRoute] int noteId)
    {
        var note = _database.Notes.Find(noteId);

        var authorizationHeader = Request.Headers["Authorization"];
        var user = BasicAuthenticationHandler.GetUserFrom(authorizationHeader);

        if (note == null)
        {
            _logger.LogWarning("User: {Username} is trying to retrive a non existing note!", user.Username);
            return NotFound($"Note with noteId {noteId} not found");
        }

        if (note.Author != user.Username)
        {
            _logger.LogWarning("Unauthorized access attempt by {Username}", user.Username);
            return Forbid("You can't access this note!");
        }

        return Ok(note);
    }

    /// <summary>
    /// Updates part of a single sticky note.
    /// </summary>
    [HttpPatch("{noteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Note> Patch([FromRoute] int noteId, [FromBody] UpdateNote patchNote)
    {
        var note = _database.Notes.Find(noteId);
        if (note == null)
        {
            return NotFound($"Note with noteId {noteId} not found");
        }

        note.Content = patchNote.Content;
        _database.SaveChanges();

        return Ok(note);
    }

    /// <summary>
    /// Deletes a single sticky note.
    /// </summary>
    [HttpDelete("{noteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Note> Delete([FromRoute] int noteId)
    {
        var note = _database.Notes.Find(noteId);
        if (note == null)
        {
            return NotFound($"Note with noteId {noteId} not found");
        }

        _database.Notes.Remove(note);
        _database.SaveChanges();

        return Ok();
    }

    /// <summary>
    /// Moves a single sticky note to another user.
    /// </summary>
    [HttpPatch("{noteId}/move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Note> Move([FromRoute] int noteId, [FromBody] MoveNote moveNote)
    {
        var note = _database.Notes.Find(noteId);
        if (note == null)
        {
            return NotFound($"Note with noteId {noteId} not found");
        }

        var authorizationHeader = Request.Headers["Authorization"];
        var user = BasicAuthenticationHandler.GetUserFrom(authorizationHeader);
        if (note.Author != user.Username)
        {
            return Forbid();
        }

        note.Author = moveNote.NewAuthor;
        _database.SaveChanges();

        return Ok(note);
    }
}