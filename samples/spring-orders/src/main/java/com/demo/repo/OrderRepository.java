package com.demo.repo;

import org.springframework.stereotype.Repository;

import com.demo.model.Order;

import java.util.ArrayList;
import java.util.List;

@Repository
public class OrderRepository {

    private final List<Order> orders = new ArrayList<>();

    public List<Order> findAll() {
        return orders;
    }

    public Order find(String id) {
        return orders.stream().filter(order -> order.getId().equals(id)).findFirst().orElse(null);
    }

    public Order save(Order order) {
        orders.add(order);
        return order;
    }
}
