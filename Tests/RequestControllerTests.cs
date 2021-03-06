﻿using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using WebAPI.Abstractions;
using WebAPI.Controllers;
using WebAPI.Models;
using Xunit;

namespace Tests
{
	public class RequestControllerTests
	{
		[Fact/*[RFC2616.10.4.1]*/]
		public void PostRequestWithNullRequestResultsInBadRequest()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			mock.Setup(m => m.Book(It.IsAny<CreateProductRequest>())).Returns(new Guid("12345678901234567890123456789012"));
			var controller = new CreateProductRequestsController(mock.Object);

			var result = controller.Post(null);

			var acceptedResult = Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact/*ALLAMARAJU.1.10*/]
		public void PostRequestResultsInAcceptedWithValidGuid()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			Product product;
			mock
				.Setup(m => m.Book(It.IsAny<CreateProductRequest>()))
				.Callback((CreateProductRequest m) => product = m.Product)
				.Returns(new Guid("12345678901234567890123456789012"));

			var controller = new CreateProductRequestsController(mock.Object);

			var result = controller.Post(new CreateProductRequest {Product = new Product {Name = "the name", Price = "66.66"}});

			var acceptedResult = Assert.IsType<AcceptedAtActionResult>(result);
			Assert.True(acceptedResult.RouteValues.ContainsKey("id"));
			Guid guid = Assert.IsType<Guid>(acceptedResult.RouteValues["id"]);
			Assert.NotEqual(Guid.Empty, guid);
		}

		[Fact/*[RFC2616.10.4.5]*/]
		public void UnknownRequestResultsInNotFound()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			mock.Setup(m => m.Lookup(It.IsAny<Guid>())).Returns(() => default(JournalEntry<CreateProductRequest>));
			var controller = new CreateProductRequestQueueController(mock.Object);
			var response = controller.Get(new Guid("12345678901234567890123456789012").ToString());
			var result = Assert.IsType<NotFoundResult>(response);
		}

		[Fact/*[RFC2616.10.4.1]*/]
		public void MissingIdResultsInBadRequest()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			var controller = new CreateProductRequestQueueController(mock.Object);
			var response = controller.Get("");
			var result = Assert.IsType<BadRequestObjectResult>(response);
			var errorResponse = Assert.IsType<ErrorResponse>(result.Value);
			Assert.Equal("missing request id", errorResponse.Error.Message.Text);
		}

		[Fact/*[RFC2616.10.4.1]*/]
		public void InvalidGuidResultsInBadRequest()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			var controller = new CreateProductRequestQueueController(mock.Object);
			var response = controller.Get("ZZZ");
			var result = Assert.IsType<BadRequestObjectResult>(response);
			var errorResponse = Assert.IsType<ErrorResponse>(result.Value);
			Assert.Equal("malformed request id", errorResponse.Error.Message.Text);
		}

		[Fact/*ALLAMARAJU.1.10*/]
		public void PendingRequestResultsInOk()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			mock.Setup(m => m.Lookup(It.IsAny<Guid>())).Returns(() =>
				new JournalEntry<CreateProductRequest>(new CreateProductRequest(), DateTime.UtcNow.AddSeconds(1),
					TransactionState.Started));
			var controller = new CreateProductRequestQueueController(mock.Object);
			var response = controller.Get(new Guid("12345678901234567890123456789012").ToString());
			var result = Assert.IsType<OkObjectResult>(response);
			var status = Assert.IsType<Status>(result.Value);
			Assert.Null(status.Link);
			Assert.NotNull(status.PingAfterDateTime);
			Assert.Equal("pending", status.State);
			Assert.Equal("Your request currently being processed.", status.Message);
		}

		[Fact/*ALLAMARAJU.1.10*/]
		public void CompletedRequestResultsInSeeOther()
		{
			var mock = new Mock<ICreateProductRequestJournal>();
			var journalEntry = new JournalEntry<CreateProductRequest>(new CreateProductRequest(), DateTime.UtcNow.AddSeconds(1),
				TransactionState.Completed);
			journalEntry.SetResult("theid");
			mock.Setup(m => m.Lookup(It.IsAny<Guid>())).Returns(() => journalEntry);
			var mockUrlHelper = new Mock<IUrlHelper>(MockBehavior.Strict);
			mockUrlHelper
				.Setup(
					x => x.Action(
						It.IsAny<UrlActionContext>()
					)
				)
				.Returns("http://example.com/")
				.Verifiable();

			var controller = new CreateProductRequestQueueController(mock.Object)
			{
				Url = mockUrlHelper.Object,
				ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext()}
			};
			var response = controller.Get(new Guid("12345678901234567890123456789012").ToString());
			var result = Assert.IsType<ObjectResult>(response);
			Assert.Equal((int) HttpStatusCode.SeeOther, result.StatusCode);
			var status = Assert.IsType<Status>(result.Value);
			Assert.NotNull(status.Link);
			Assert.Null(status.PingAfterDateTime);
			Assert.Equal("done", status.State);
			Assert.Equal("Your request has been processed.", status.Message);
		}
	}
}