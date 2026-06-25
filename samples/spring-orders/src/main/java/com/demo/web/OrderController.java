package com.demo.web;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

import com.demo.service.OrderService;
import com.demo.model.Order;

import java.util.List;

@RestController
public class OrderController {

    private final OrderService orderService;

    public OrderController(OrderService orderService) {
        this.orderService = orderService;
    }

    @GetMapping("/orders")
    public List<Order> list() {
        return orderService.findAll();
    }

    @GetMapping("/orders/{id}")
    public Order get(@PathVariable String id) {
        return orderService.find(id);
    }

    @PostMapping("/orders")
    public Order create(@RequestBody Order order) {
        return orderService.create(order);
    }
}
