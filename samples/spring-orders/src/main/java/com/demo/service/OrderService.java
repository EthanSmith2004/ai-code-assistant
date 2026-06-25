package com.demo.service;

import org.springframework.stereotype.Service;

import com.demo.repo.OrderRepository;
import com.demo.model.Order;

import java.util.List;

@Service
public class OrderService {

    private final OrderRepository orderRepository;

    public OrderService(OrderRepository orderRepository) {
        this.orderRepository = orderRepository;
    }

    public List<Order> findAll() {
        return orderRepository.findAll();
    }

    public Order find(String id) {
        return orderRepository.find(id);
    }

    public Order create(Order order) {
        return orderRepository.save(order);
    }
}
