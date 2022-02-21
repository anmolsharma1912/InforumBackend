﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InforumBackend.Data;
using InforumBackend.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace InforumBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogPostsController : ControllerBase
    {
        private readonly InforumBackendContext _context;

        public BlogPostsController(InforumBackendContext context)
        {
            _context = context;
        }

        // GET: api/BlogPosts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BlogPost>>> GetBlogPost([FromQuery] PageParameter pageParameter, string userId, Boolean starSort)
        {
            try
            {
                IOrderedQueryable<BlogPost> blogPosts;

                if (starSort && !String.IsNullOrEmpty(userId))
                {
                    blogPosts = _context.BlogPost.Where(bp => bp.AuthorId == userId).Include(bp => bp.Category).OrderByDescending(bp => bp.Star);
                }
                else if (!String.IsNullOrEmpty(userId))
                {
                    blogPosts = _context.BlogPost.Where(bp => bp.AuthorId == userId).Include(bp => bp.Category).OrderByDescending(bp => bp.DatePosted);
                }
                else if (starSort)
                {
                    blogPosts = _context.BlogPost.Include(bp => bp.Category).OrderByDescending(bp => bp.Star);
                }
                else
                {
                    blogPosts = _context.BlogPost.Include(bp => bp.Category).OrderByDescending(bp => bp.DatePosted);
                }

                var paginationMetadata = new PaginationMetadata(blogPosts.Count(), pageParameter.PageNumber, pageParameter.PageSize);
                Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationMetadata));

                var posts = await blogPosts.Skip((pageParameter.PageNumber - 1) * pageParameter.PageSize).Take(pageParameter.PageSize).ToListAsync();

                return Ok(new
                {
                    posts = posts,
                    pagination = paginationMetadata
                });
            }
            catch (System.Exception)
            {

                return BadRequest();
            }
        }

        // GET: api/BlogPosts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BlogPost>> GetBlogPost(long id)
        {
            try
            {
                var blogPost = await _context.BlogPost.Include(bp => bp.Category).FirstOrDefaultAsync(i => i.Id == id);

                if (blogPost == null)
                {
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found"
                    });
                }

                return Ok(blogPost);
            }
            catch (System.Exception)
            {

                return BadRequest();
            }
        }

        // GET: api/BlogPosts/slug/5
        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<BlogPost>> GetBlogPostBySlug(string slug)
        {
            try
            {
                var blogPost = await _context.BlogPost.Include(bp => bp.Category).FirstOrDefaultAsync(i => i.Slug == slug);

                if (blogPost == null)
                {
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found"
                    });
                }

                return Ok(blogPost);
            }
            catch (System.Exception)
            {
                return BadRequest();
            }
        }

        // PUT: api/BlogPosts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Editor, Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBlogPost(long id, BlogPost blogPost)
        {
            if (id != blogPost.Id)
            {
                return BadRequest(new
                {
                    Status = StatusCodes.Status400BadRequest,
                    Message = "Check if the Post data is Valid or not."
                });
            }

            // generate slug based on the PUT data
            blogPost.Slug = generateSlug(blogPost.Title, id);

            _context.Entry(blogPost).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = StatusCodes.Status200OK,
                    Message = "Post Updated Successfully."
                });
            }
            catch (System.Exception)
            {
                if (!BlogPostExists(id))
                {
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post Does not Exist"
                    });
                }
                else
                {
                    return BadRequest();
                }
            }
        }

        // POST: api/BlogPosts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Editor, Admin")]
        [HttpPost]
        public async Task<ActionResult<BlogPost>> PostBlogPost(BlogPost blogPost)
        {
            try
            {
                // add a new BlogPost object
                _context.BlogPost.Add(blogPost);
                await _context.SaveChangesAsync(); // save the object

                // Get Title and Id from the saved BlogPost object and generate slug
                var slug = generateSlug(blogPost.Title, blogPost.Id);

                // assign generated slug to the BlogPost object
                blogPost.Slug = slug;

                // update and save the object
                _context.Update(blogPost);

                await _context.SaveChangesAsync();

                return StatusCode(StatusCodes.Status201Created, new
                {
                    Status = StatusCodes.Status201Created,
                    Message = "Post created Sucessfully."
                });
            }
            catch (System.Exception)
            {
                return BadRequest();
            }
        }

        // DELETE: api/BlogPosts/5
        [Authorize(Roles = "Editor, Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlogPost(long id)
        {
            var blogPost = await _context.BlogPost.FindAsync(id);
            if (blogPost == null)
            {
                return NotFound(new
                {
                    Status = StatusCodes.Status404NotFound,
                    Message = "Post not found"

                });
            }

            try
            {
                _context.BlogPost.Remove(blogPost);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = StatusCodes.Status200OK,
                    Message = "Post deleted successfully"
                });
            }
            catch (System.Exception)
            {

                return BadRequest();
            }
        }

        // POST: api/BlogPosts/star
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize]
        [HttpPost("star")]
        public async Task<IActionResult> StarBlogPost(Star starModel)
        {
            try
            {
                // Find if the Blog Post Exist or not
                var blogPost = await _context.BlogPost.FindAsync(starModel.BlogPostId);

                // Return 404 if the Blog Post does not exist
                // Else Continue
                if (blogPost == null)
                {
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found."
                    });
                }

                // Check if the Star Entry Exist or not
                var starExist = _context.Star.Any(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId);

                // If Star Entry Exist remove the Star Entry
                if (starExist)
                {
                    // Find and remove the Star Entry
                    var removeStar = _context.Star.Remove(_context.Star.FirstOrDefault(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId));

                    // -1 the Star count if Star Entry Successfully Removed
                    if (removeStar != null)
                    {
                        blogPost.Star--;
                    }

                    // Save Changes to the Database
                    await _context.SaveChangesAsync();

                    // Retutrn OK
                    return Ok(new
                    {
                        Status = StatusCodes.Status200OK,
                        Message = "Star removed Successfully."
                    });
                }
                // Else Add the Star Entry
                else
                {
                    // Add the Star Entry
                    var addStar = _context.Star.Add(starModel);

                    // +1 the count if Star Entry Successfully Added
                    if (addStar != null)
                    {
                        blogPost.Star++;
                    }

                    // Save Changes to the Database
                    await _context.SaveChangesAsync();

                    // Retutrn OK
                    return Ok(new
                    {
                        Status = StatusCodes.Status200OK,
                        Message = "Star added Successfully."
                    });
                }
            }
            catch (System.Exception)
            {
                return BadRequest();
            }
        }

        // POST: api/BlogPosts/star/status
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize]
        [HttpPost("star/status")]
        public async Task<IActionResult> StarStatusBlogPost(Star starModel)
        {
            try
            {
                // Find if the Blog Post Exist or not
                var blogPost = await _context.BlogPost.FindAsync(starModel.BlogPostId);

                // Return 404 if the Blog Post does not exist
                // Else Continue
                if (blogPost == null)
                {
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found."
                    });
                }

                // Check if the Star Entry Exist or not
                var starExist = _context.Star.Any(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId);

                // Retutrn OK
                return Ok(new
                {
                    StarExist = starExist
                });

            }
            catch (System.Exception)
            {
                return BadRequest();
            }
        }

        private bool BlogPostExists(long id)
        {
            return _context.BlogPost.Any(e => e.Id == id);
        }

        /// <summary>
        /// Method to generate a slug from title and id from the BlogPost object
        /// removes all the special characters and spaces and replaces them with dashes(-)
        /// concatenates cleaned title and id and returns the slug in format of title-id
        /// </summary>
        /// <param name="title"></param>
        /// <param name="id"></param>
        /// <returns>slug(title-id)</returns>
        private string generateSlug(string title, long id)
        {
            var slug = title.ToLower();

            // remove all uneeded characters
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            // remove multiple spaces
            slug = Regex.Replace(slug, @"\s+", " ").Trim();
            // replace spaces with dashes(-)
            slug = Regex.Replace(slug, @"\s", "-");
            // concatenate slug and id
            slug = slug + "-" + id;

            return slug;
        }
    }
}
