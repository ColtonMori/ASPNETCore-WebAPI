﻿using System;
using System.Linq;
using AutoMapper;
using SampleWebApiAspNetCore.Dtos;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using SampleWebApiAspNetCore.Repositories;
using System.Collections.Generic;
using SampleWebApiAspNetCore.Entities;
using SampleWebApiAspNetCore.Models;
using SampleWebApiAspNetCore.Helpers;
using SampleWebApiAspNetCore.Services;
using System.Text.Json;

namespace SampleWebApiAspNetCore.v1.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    //[Route("api/[controller]")]
    public class FoodsController : ControllerBase
    {
        private readonly IFoodRepository _foodRepository;
        private readonly IUrlHelper _urlHelper;
        private readonly IMapper _mapper;
        private readonly ILinkService<FoodsController> _linkService;

        public FoodsController(
            IUrlHelper urlHelper,
            IFoodRepository foodRepository,
            IMapper mapper,
            ILinkService<FoodsController> linkService)
        {
            _foodRepository = foodRepository;
            _mapper = mapper;
            _linkService = linkService;
        }

        [HttpGet(Name = nameof(GetAllFoods))]
        public ActionResult GetAllFoods(ApiVersion version, [FromQuery] QueryParameters queryParameters)
        {
            List<FoodEntity> foodItems = _foodRepository.GetAll(queryParameters).ToList();

            var allItemCount = _foodRepository.Count();

            var paginationMetadata = new
            {
                totalCount = allItemCount,
                pageSize = queryParameters.PageCount,
                currentPage = queryParameters.Page,
                totalPages = queryParameters.GetTotalPages(allItemCount)
            };

            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(paginationMetadata));

            var links = _linkService.CreateLinksForCollection(queryParameters, allItemCount, version);
            var toReturn = foodItems.Select(x => _linkService.ExpandSingleFoodItem(x, x.Id, version));

            return Ok(new
            {
                value = toReturn,
                links = links
            });
        }

        [HttpGet]
        [Route("{id:int}", Name = nameof(GetSingleFood))]
        public ActionResult GetSingleFood(ApiVersion version, int id)
        {
            FoodEntity foodItem = _foodRepository.GetSingle(id);

            if (foodItem == null)
            {
                return NotFound();
            }

            FoodDto item = _mapper.Map<FoodDto>(foodItem);

            return Ok(_linkService.ExpandSingleFoodItem(item, item.Id, version));
        }

        [HttpPost(Name = nameof(AddFood))]
        public ActionResult<FoodDto> AddFood(ApiVersion version, [FromBody] FoodCreateDto foodCreateDto)
        {
            if (foodCreateDto == null)
            {
                return BadRequest();
            }

            FoodEntity toAdd = _mapper.Map<FoodEntity>(foodCreateDto);

            _foodRepository.Add(toAdd);

            if (!_foodRepository.Save())
            {
                throw new Exception("Creating a fooditem failed on save.");
            }

            FoodEntity newFoodItem = _foodRepository.GetSingle(toAdd.Id);

            return CreatedAtRoute(nameof(GetSingleFood),
                new { version = version.ToString(), id = newFoodItem.Id },
                _mapper.Map<FoodDto>(newFoodItem));
        }

        [HttpPatch("{id:int}", Name = nameof(PartiallyUpdateFood))]
        public ActionResult<FoodDto> PartiallyUpdateFood(int id, [FromBody] JsonPatchDocument<FoodUpdateDto> patchDoc)
        {
            if (patchDoc == null)
            {
                return BadRequest();
            }

            FoodEntity existingEntity = _foodRepository.GetSingle(id);

            if (existingEntity == null)
            {
                return NotFound();
            }

            FoodUpdateDto foodUpdateDto = _mapper.Map<FoodUpdateDto>(existingEntity);
            patchDoc.ApplyTo(foodUpdateDto);

            TryValidateModel(foodUpdateDto);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _mapper.Map(foodUpdateDto, existingEntity);
            FoodEntity updated = _foodRepository.Update(id, existingEntity);

            if (!_foodRepository.Save())
            {
                throw new Exception("Updating a fooditem failed on save.");
            }

            return Ok(_mapper.Map<FoodDto>(updated));
        }

        [HttpDelete]
        [Route("{id:int}", Name = nameof(RemoveFood))]
        public ActionResult RemoveFood(int id)
        {
            FoodEntity foodItem = _foodRepository.GetSingle(id);

            if (foodItem == null)
            {
                return NotFound();
            }

            _foodRepository.Delete(id);

            if (!_foodRepository.Save())
            {
                throw new Exception("Deleting a fooditem failed on save.");
            }

            return NoContent();
        }

        [HttpPut]
        [Route("{id:int}", Name = nameof(UpdateFood))]
        public ActionResult<FoodDto> UpdateFood(int id, [FromBody] FoodUpdateDto foodUpdateDto)
        {
            if (foodUpdateDto == null)
            {
                return BadRequest();
            }

            var existingFoodItem = _foodRepository.GetSingle(id);

            if (existingFoodItem == null)
            {
                return NotFound();
            }

            _mapper.Map(foodUpdateDto, existingFoodItem);

            _foodRepository.Update(id, existingFoodItem);

            if (!_foodRepository.Save())
            {
                throw new Exception("Updating a fooditem failed on save.");
            }

            return Ok(_mapper.Map<FoodDto>(existingFoodItem));
        }

        [HttpGet("GetRandomMeal", Name = nameof(GetRandomMeal))]
        public ActionResult GetRandomMeal()
        {
            ICollection<FoodEntity> foodItems = _foodRepository.GetRandomMeal();

            IEnumerable<FoodDto> dtos = foodItems.Select(x => _mapper.Map<FoodDto>(x));

            var links = new List<LinkDto>();

            // self 
            links.Add(new LinkDto(_urlHelper.Link(nameof(GetRandomMeal), null), "self", "GET"));

            return Ok(new
            {
                value = dtos,
                links = links
            });
        }
    }
}
